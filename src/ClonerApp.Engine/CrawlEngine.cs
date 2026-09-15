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
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _running = new();

    public event EventHandler<RunProgress>? ProgressChanged;

    public CrawlEngine(IServiceScopeFactory scopeFactory, ILogger<CrawlEngine> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public bool IsRunning(Guid projectId) => _running.ContainsKey(projectId);

    public void Cancel(Guid projectId)
    {
        if (_running.TryGetValue(projectId, out var cts))
            cts.Cancel();
    }

    public async Task RunProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!_running.TryAdd(projectId, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)))
            throw new InvalidOperationException("Project is already running.");

        var linked = _running[projectId];
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var runs = scope.ServiceProvider.GetRequiredService<IRunRepository>();
            var crawler = scope.ServiceProvider.GetRequiredService<SiteCrawler>();
            var filter = scope.ServiceProvider.GetRequiredService<MediaFilter>();
            var downloader = scope.ServiceProvider.GetRequiredService<MediaDownloader>();

            var project = await projects.GetByIdAsync(projectId, linked.Token)
                ?? throw new InvalidOperationException("Project not found.");

            var run = await runs.CreateAsync(new CrawlRun
            {
                ProjectId = projectId,
                Status = RunStatus.Running,
                StartedAtUtc = DateTime.UtcNow
            }, linked.Token);

            Report(projectId, run.Id, "Starting crawl…", run);

            try
            {
                var urls = UrlSeedExpander.Expand(project);
                if (urls.Count == 0)
                    throw new InvalidOperationException("No valid start URLs after expansion/filters.");

                var crawlProgress = new Progress<string>(msg => Report(projectId, run.Id, msg, run));

                var crawlResult = await crawler.CrawlAsync(
                    urls,
                    project.MaxDepth,
                    project.MaxPages,
                    project.SameDomainOnly,
                    project.PageDelayMs,
                    project.ScanWithinStartingFolder,
                    project.IgnoreHomePage,
                    project.AlwaysScanImageLinks,
                    project.UrlRegex,
                    crawlProgress,
                    linked.Token);

                run.PagesCrawled = crawlResult.PagesCrawled;
                var filteredMedia = filter.FilterByExtension(crawlResult.Media, project);
                run.MediaFound = filteredMedia.Count;
                Report(projectId, run.Id, $"Found {filteredMedia.Count} media files on {crawlResult.PagesCrawled} pages", run);

                var channel = Channel.CreateUnbounded<MediaCandidate>();
                foreach (var item in filteredMedia)
                    channel.Writer.TryWrite(item);
                channel.Writer.Complete();

                var workers = Math.Clamp(project.MaxConnections, 1, 16);
                var downloaded = 0;
                var skipped = 0;
                var filtered = 0;
                var failed = 0;

                var tasks = Enumerable.Range(0, workers).Select(async _ =>
                {
                    await foreach (var candidate in channel.Reader.ReadAllAsync(linked.Token))
                    {
                        var outcome = await downloader.DownloadAsync(project, candidate, linked.Token);
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
                                Report(projectId, run.Id, $"Failed: {candidate.Url} — {outcome.Message}", run);
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
                Report(projectId, run.Id, "Cancelled", run, completed: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Run failed for project {ProjectId}", projectId);
                run.Status = RunStatus.Failed;
                run.ErrorMessage = ex.Message;
                run.EndedAtUtc = DateTime.UtcNow;
                await runs.UpdateAsync(run, CancellationToken.None);
                Report(projectId, run.Id, $"Failed: {ex.Message}", run, failed: true);
            }
        }
        finally
        {
            if (_running.TryRemove(projectId, out var cts))
                cts.Dispose();
        }
    }

    private void Report(Guid projectId, Guid runId, string message, CrawlRun run, bool completed = false, bool failed = false)
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
            IsFailed = failed
        });
    }
}
