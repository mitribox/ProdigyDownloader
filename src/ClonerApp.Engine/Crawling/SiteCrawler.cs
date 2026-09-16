using System.Collections.Concurrent;
using System.Net;
using ClonerApp.Core.Interfaces;
using ClonerApp.Core.Models;
using Microsoft.Extensions.Logging;

namespace ClonerApp.Engine.Crawling;

public sealed class SiteCrawler
{
    private readonly HttpClient _httpClient;
    private readonly HtmlMediaExtractor _extractor;
    private readonly ILogger<SiteCrawler> _logger;

    public SiteCrawler(HttpClient httpClient, HtmlMediaExtractor extractor, ILogger<SiteCrawler> logger)
    {
        _httpClient = httpClient;
        _extractor = extractor;
        _logger = logger;
    }

    public async Task<CrawlResult> CrawlAsync(
        IEnumerable<string> startUrls,
        int maxDepth,
        int maxPages,
        bool sameDomainOnly,
        int pageDelayMs,
        bool scanWithinStartingFolder,
        bool ignoreHomePage,
        bool alwaysScanImageLinks,
        string? urlRegex,
        IProgress<string>? progress,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task>? waitIfPaused = null,
        bool incrementalWatch = false,
        Guid? projectId = null,
        ICrawledPageRepository? pageRepository = null)
    {
        var seeds = startUrls
            .Select(u => u.Trim())
            .Where(u => Uri.TryCreate(u, UriKind.Absolute, out _))
            .Select(UrlNormalizer.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (seeds.Count == 0)
            throw new InvalidOperationException("No valid start URLs provided.");

        var allowedHosts = seeds
            .Select(s => new Uri(s).Host)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seedSet = seeds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var visited = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string Url, int Depth)>();
        foreach (var seed in seeds)
            queue.Enqueue((seed, 0));

        var media = new Dictionary<string, MediaCandidate>(StringComparer.OrdinalIgnoreCase);
        var pagesCrawled = 0;
        var pagesSkippedUnchanged = 0;

        while (queue.Count > 0 && pagesCrawled < maxPages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (waitIfPaused is not null)
                await waitIfPaused(cancellationToken).ConfigureAwait(false);

            var (url, depth) = queue.Dequeue();
            if (!visited.TryAdd(url, 0))
                continue;

            if (sameDomainOnly && !allowedHosts.Contains(new Uri(url).Host))
                continue;

            if (ignoreHomePage && !seedSet.Contains(url) && CrawlScopeHelper.IsHomePage(url))
                continue;

            if (scanWithinStartingFolder && !CrawlScopeHelper.IsWithinStartingFolder(url, seeds))
                continue;

            if (!CrawlScopeHelper.MatchesRegex(url, urlRegex) && !seedSet.Contains(url))
                continue;

            progress?.Report($"Crawling: {url}");
            _logger.LogInformation("Crawling {Url} at depth {Depth}", url, depth);

            try
            {
                CrawledPage? prior = null;
                if (incrementalWatch && projectId is Guid pid && pageRepository is not null)
                    prior = await pageRepository.GetByUrlAsync(pid, url, cancellationToken);

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (incrementalWatch && prior is not null)
                {
                    if (!string.IsNullOrEmpty(prior.ETag))
                        request.Headers.TryAddWithoutValidation("If-None-Match", prior.ETag);
                    if (!string.IsNullOrEmpty(prior.LastModified) &&
                        DateTimeOffset.TryParse(prior.LastModified, out var lm))
                    {
                        request.Headers.IfModifiedSince = lm;
                    }
                }

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    pagesSkippedUnchanged++;
                    if (prior is not null && projectId is Guid p304 && pageRepository is not null)
                    {
                        prior.LastSeenAtUtc = DateTime.UtcNow;
                        await pageRepository.UpsertAsync(prior, cancellationToken);
                    }
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Skip {Url}: HTTP {Status}", url, (int)response.StatusCode);
                    continue;
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase) &&
                    !contentType.Contains("text/", StringComparison.OrdinalIgnoreCase) &&
                    contentType.Length > 0)
                {
                    continue;
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                pagesCrawled++;

                var etag = response.Headers.ETag?.Tag;
                var lastModified = response.Content.Headers.LastModified?.ToString("R");
                var hash = SitemapDiscoverer.ComputeContentHash(html);
                var contentChanged = prior is null ||
                    !string.Equals(prior.ContentHash, hash, StringComparison.OrdinalIgnoreCase);

                if (incrementalWatch && projectId is Guid projectKey && pageRepository is not null)
                {
                    await pageRepository.UpsertAsync(new CrawledPage
                    {
                        ProjectId = projectKey,
                        Url = url,
                        ETag = etag,
                        LastModified = lastModified,
                        ContentHash = hash,
                        LastSeenAtUtc = DateTime.UtcNow
                    }, cancellationToken);
                }

                var pageUri = new Uri(url);
                var extracted = _extractor.Extract(html, pageUri, alwaysScanImageLinks);

                // Watch: only collect media from new or changed pages
                if (!incrementalWatch || contentChanged)
                {
                    foreach (var item in extracted.Media)
                        media.TryAdd(item.Url, item);
                }
                else
                {
                    pagesSkippedUnchanged++;
                }

                if (depth < maxDepth)
                {
                    foreach (var link in extracted.PageLinks)
                    {
                        if (sameDomainOnly && !allowedHosts.Contains(new Uri(link).Host))
                            continue;
                        if (ignoreHomePage && CrawlScopeHelper.IsHomePage(link))
                            continue;
                        if (scanWithinStartingFolder && !CrawlScopeHelper.IsWithinStartingFolder(link, seeds))
                            continue;
                        if (!CrawlScopeHelper.MatchesRegex(link, urlRegex))
                            continue;
                        if (!visited.ContainsKey(link))
                            queue.Enqueue((link, depth + 1));
                    }
                }

                if (pageDelayMs > 0)
                    await Task.Delay(pageDelayMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to crawl {Url}", url);
            }
        }

        return new CrawlResult(pagesCrawled, media.Values.ToList(), pagesSkippedUnchanged);
    }
}

public sealed record CrawlResult(
    int PagesCrawled,
    IReadOnlyList<MediaCandidate> Media,
    int PagesSkippedUnchanged = 0);
