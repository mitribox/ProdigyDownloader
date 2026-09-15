using System.Collections.Concurrent;
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
        CancellationToken cancellationToken)
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

        while (queue.Count > 0 && pagesCrawled < maxPages)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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

                var pageUri = new Uri(url);
                var extracted = _extractor.Extract(html, pageUri, alwaysScanImageLinks);

                foreach (var item in extracted.Media)
                    media.TryAdd(item.Url, item);

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

        return new CrawlResult(pagesCrawled, media.Values.ToList());
    }
}

public sealed record CrawlResult(int PagesCrawled, IReadOnlyList<MediaCandidate> Media);
