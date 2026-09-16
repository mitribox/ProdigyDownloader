using System.Collections.Concurrent;
using System.Threading.Channels;
using ClonerApp.Core.Enums;
using ClonerApp.Core.Interfaces;
using ClonerApp.Core.Models;
using ClonerApp.Engine.Crawling;
using ClonerApp.Engine.Downloading;
using ClonerApp.Engine.Filtering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClonerApp.Engine;

public sealed class CrawlEngine : ICrawlEngine
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CrawlEngine> _logger;
    private readonly ConcurrentDictionary<Guid, RunControl> _running = new();

    public event EventHandler<RunProgress>? ProgressChanged;

    public CrawlEngine(IServiceScopeFactory scopeFactory, ILogger<CrawlEngine> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public bool IsRunning(Guid projectId) => _running.ContainsKey(projectId);

    public bool IsPaused(Guid projectId) =>
        _running.TryGetValue(projectId, out var control) && control.IsPaused;

    public void Pause(Guid projectId)
    {
        if (!_running.TryGetValue(projectId, out var control) || control.IsPaused)
            return;

        control.Pause();
        ProgressChanged?.Invoke(this, new RunProgress
        {
            ProjectId = projectId,
            Message = "Paused",
            IsPaused = true
        });
    }

    public void Resume(Guid projectId)
    {
        if (!_running.TryGetValue(projectId, out var control) || !control.IsPaused)
            return;

        control.Resume();
        ProgressChanged?.Invoke(this, new RunProgress
        {
            ProjectId = projectId,
            Message = "Resumed",
            IsPaused = false
        });
    }

    public void Cancel(Guid projectId)
    {
        if (_running.TryGetValue(projectId, out var control))
            control.Cancel();
    }

    public async Task RunProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var control = new RunControl(cancellationToken);
        if (!_running.TryAdd(projectId, control))
        {
            control.Dispose();
            throw new InvalidOperationException("Project is already running.");
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var runs = scope.ServiceProvider.GetRequiredService<IRunRepository>();
            var crawler = scope.ServiceProvider.GetRequiredService<SiteCrawler>();
            var sitemap = scope.ServiceProvider.GetRequiredService<SitemapDiscoverer>();
            var filter = scope.ServiceProvider.GetRequiredService<MediaFilter>();
            var downloader = scope.ServiceProvider.GetRequiredService<MediaDownloader>();
            var pageRepo = scope.ServiceProvider.GetRequiredService<ICrawledPageRepository>();

            var project = await projects.GetByIdAsync(projectId, control.Token)
                ?? throw new InvalidOperationException("Project not found.");

            var run = await runs.CreateAsync(new CrawlRun
            {
                ProjectId = projectId,
                Status = RunStatus.Running,
                StartedAtUtc = DateTime.UtcNow
            }, control.Token);

            var isWatch = project.RunMode == RunMode.Monitor;
            var startMessage = project.CrawlEntireSite
                ? (isWatch ? "Starting full-site Watch…" : "Starting full-site crawl…")
                : (isWatch ? "Starting Watch for new posts…" : "Starting crawl…");
            Report(projectId, run.Id, startMessage, run);

            try
            {
                var urls = UrlSeedExpander.Expand(project).ToList();
                if (urls.Count == 0)
                    throw new InvalidOperationException("No valid start URLs after expansion/filters.");

                var (maxDepth, maxPages, sameDomainOnly, scanWithinFolder) = CrawlPresets.ResolveScope(
                    project.CrawlEntireSite,
                    project.MaxDepth,
                    project.MaxPages,
                    project.SameDomainOnly,
                    project.ScanWithinStartingFolder);

                if (project.CrawlEntireSite || isWatch)
                {
                    Report(projectId, run.Id, "Discovering sitemap URLs…", run);
                    var fromSitemap = await sitemap.DiscoverAsync(urls, control.Token);
                    if (fromSitemap.Count > 0)
                    {
                        var merged = urls
                            .Concat(fromSitemap)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        urls = merged;
                        Report(projectId, run.Id, $"Merged {fromSitemap.Count} sitemap URL(s); {urls.Count} seeds total", run);
                    }
                }

                var crawlProgress = new Progress<string>(msg => Report(projectId, run.Id, msg, run));

                var crawlResult = await crawler.CrawlAsync(
                    urls,
                    maxDepth,
                    maxPages,
                    sameDomainOnly,
                    project.PageDelayMs,
                    scanWithinFolder,
                    project.IgnoreHomePage,
                    project.AlwaysScanImageLinks,
                    project.UrlRegex,
                    crawlProgress,
                    control.Token,
                    control.WaitIfPausedAsync,
                    incrementalWatch: isWatch,
                    projectId: project.Id,
                    pageRepository: pageRepo);

                control.Token.ThrowIfCancellationRequested();
                await control.WaitIfPausedAsync(control.Token);

                run.PagesCrawled = crawlResult.PagesCrawled;
                var filteredMedia = filter.FilterByExtension(crawlResult.Media, project);
                var excludeRules = ExcludeRuleEvaluator.ParseRules(project.ExcludeRulesJson);
                var afterExclude = filteredMedia
                    .Where(c => !ExcludeRuleEvaluator.ShouldExclude(c, excludeRules))
                    .ToList();
                var excludedCount = filteredMedia.Count - afterExclude.Count;
                run.MediaFound = afterExclude.Count;
                run.Filtered = excludedCount;
                Report(projectId, run.Id,
                    $"Found {afterExclude.Count} media file(s) on {crawlResult.PagesCrawled} page(s)" +
                    (crawlResult.PagesSkippedUnchanged > 0 ? $", skipped {crawlResult.PagesSkippedUnchanged} unchanged" : "") +
                    (excludedCount > 0 ? $", {excludedCount} excluded by rules" : ""),
                    run);

                var channel = Channel.CreateUnbounded<MediaCandidate>();
                foreach (var item in afterExclude)
                    channel.Writer.TryWrite(item);
                channel.Writer.Complete();

                var workers = Math.Clamp(project.MaxConnections, 1, 16);
                var downloaded = 0;
                var skipped = 0;
                var filtered = excludedCount;
                var failed = 0;

                var tasks = Enumerable.Range(0, workers).Select(async _ =>
                {
                    await foreach (var candidate in channel.Reader.ReadAllAsync(control.Token))
                    {
                        await control.WaitIfPausedAsync(control.Token);
                        control.Token.ThrowIfCancellationRequested();

                        var outcome = await downloader.DownloadAsync(project, candidate, control.Token);
                        switch (outcome.Status)
                        {
                            case AssetStatus.Downloaded:
                                Interlocked.Increment(ref downloaded);
                                run.Downloaded = downloaded;
                                Report(projectId, run.Id, $"Downloaded: {candidate.Url}", run);
                                break;
                            case AssetStatus.Skipped:
                                Interlocked.Increment(ref skipped);
                                run.Skipped = skipped;
                                break;
                            case AssetStatus.Filtered:
                                Interlocked.Increment(ref filtered);
                                run.Filtered = filtered;
                                break;
                            default:
                                Interlocked.Increment(ref failed);
                                run.Failed = failed;
                                Report(projectId, run.Id, $"Failed: {candidate.Url} - {outcome.Message}", run);
                                break;
                        }
                    }
                });

                await Task.WhenAll(tasks);

                run.Downloaded = downloaded;
                run.Skipped = skipped;
                run.Filtered = filtered;
                run.Failed = failed;
                run.Status = RunStatus.Completed;
                run.EndedAtUtc = DateTime.UtcNow;
                await runs.UpdateAsync(run, CancellationToken.None);

                project.LastRunAtUtc = DateTime.UtcNow;
                project.NextRunAtUtc = ScheduleCalculator.CalculateNextRun(project, DateTime.UtcNow);
                await projects.UpdateAsync(project, CancellationToken.None);

                Report(projectId, run.Id,
                    $"Completed. Downloaded {run.Downloaded}, skipped {run.Skipped}, filtered {run.Filtered}, failed {run.Failed}",
                    run, completed: true);
            }
            catch (OperationCanceledException)
            {
                run.Status = RunStatus.Cancelled;
                run.EndedAtUtc = DateTime.UtcNow;
                await runs.UpdateAsync(run, CancellationToken.None);
                await AdvanceNextRunAsync(projects, project);
                Report(projectId, run.Id, "Cancelled", run, cancelled: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Run failed for project {ProjectId}", projectId);
                run.Status = RunStatus.Failed;
                run.ErrorMessage = ex.Message;
                run.EndedAtUtc = DateTime.UtcNow;
                await runs.UpdateAsync(run, CancellationToken.None);
                await AdvanceNextRunAsync(projects, project);
                Report(projectId, run.Id, $"Failed: {ex.Message}", run, failed: true);
            }
        }
        finally
        {
            if (_running.TryRemove(projectId, out var removed))
                removed.Dispose();
        }
    }

    private static async Task AdvanceNextRunAsync(IProjectRepository projects, Project project)
    {
        if (project.RunMode == RunMode.Once)
            return;

        project.NextRunAtUtc = ScheduleCalculator.CalculateNextRun(project, DateTime.UtcNow);
        await projects.UpdateAsync(project, CancellationToken.None);
    }

    private void Report(
        Guid projectId,
        Guid runId,
        string message,
        CrawlRun run,
        bool completed = false,
        bool failed = false,
        bool cancelled = false)
    {
        ProgressChanged?.Invoke(this, new RunProgress
        {
            ProjectId = projectId,
            RunId = runId,
            Message = message,
            PagesCrawled = run.PagesCrawled,
            MediaFound = run.MediaFound,
            Downloaded = run.Downloaded,
            Skipped = run.Skipped,
            Failed = run.Failed,
            Filtered = run.Filtered,
            IsCompleted = completed,
            IsFailed = failed,
            IsCancelled = cancelled,
            IsPaused = IsPaused(projectId)
        });
    }
}
