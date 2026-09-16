using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace ClonerApp.Engine.Crawling;

public sealed class SitemapDiscoverer
{
    private static readonly XNamespace Sm = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private const int MaxSitemapDocuments = 20;
    private const int MaxUrls = 20_000;

    private readonly HttpClient _httpClient;
    private readonly ILogger<SitemapDiscoverer> _logger;

    public SitemapDiscoverer(HttpClient httpClient, ILogger<SitemapDiscoverer> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> DiscoverAsync(
        IEnumerable<string> seedUrls,
        CancellationToken cancellationToken = default)
    {
        var hosts = seedUrls
            .Select(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) ? uri : null)
            .Where(u => u is not null)
            .Cast<Uri>()
            .GroupBy(u => u.GetLeftPart(UriPartial.Authority), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var origin in hosts)
        {
            foreach (var sitemapUrl in await ResolveSitemapLocationsAsync(origin, cancellationToken))
                await CollectFromSitemapAsync(sitemapUrl, origin.Host, found, depth: 0, cancellationToken);
        }

        return found.Take(MaxUrls).ToList();
    }

    /// <summary>Parse sitemap XML text (urlset or sitemapindex). Used by tests and discovery.</summary>
    public static IReadOnlyList<string> ParseSitemapXml(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root;
        if (root is null) return Array.Empty<string>();

        var ns = root.Name.Namespace;
        var local = root.Name.LocalName;
        if (local.Equals("sitemapindex", StringComparison.OrdinalIgnoreCase))
        {
            return root.Elements(ns + "sitemap")
                .Select(e => (string?)e.Element(ns + "loc"))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .ToList();
        }

        if (local.Equals("urlset", StringComparison.OrdinalIgnoreCase))
        {
            return root.Elements(ns + "url")
                .Select(e => (string?)e.Element(ns + "loc"))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .ToList();
        }

        // Namespace-agnostic fallback
        return root.Descendants()
            .Where(e => e.Name.LocalName.Equals("loc", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Value.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<string>> ResolveSitemapLocationsAsync(Uri origin, CancellationToken cancellationToken)
    {
        var locations = new List<string>();
        try
        {
            var robotsUrl = new Uri(origin, "/robots.txt");
            using var response = await _httpClient.GetAsync(robotsUrl, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                foreach (var line in text.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("Sitemap:", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = trimmed["Sitemap:".Length..].Trim();
                        if (Uri.TryCreate(value, UriKind.Absolute, out _))
                            locations.Add(value);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "robots.txt sitemap discovery failed for {Host}", origin.Host);
        }

        locations.Add(new Uri(origin, "/sitemap.xml").AbsoluteUri);
        locations.Add(new Uri(origin, "/sitemap_index.xml").AbsoluteUri);
        return locations.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task CollectFromSitemapAsync(
        string sitemapUrl,
        string allowedHost,
        HashSet<string> found,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth > 3 || found.Count >= MaxUrls)
            return;

        try
        {
            using var response = await _httpClient.GetAsync(sitemapUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return;

            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(xml))
                return;

            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            if (root is null) return;

            var ns = root.Name.Namespace;
            if (root.Name.LocalName.Equals("sitemapindex", StringComparison.OrdinalIgnoreCase))
            {
                var children = root.Elements(ns + "sitemap")
                    .Select(e => (string?)e.Element(ns + "loc"))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!.Trim())
                    .Take(MaxSitemapDocuments)
                    .ToList();

                foreach (var child in children)
                    await CollectFromSitemapAsync(child, allowedHost, found, depth + 1, cancellationToken);
                return;
            }

            foreach (var loc in ParseSitemapXml(xml))
            {
                if (!Uri.TryCreate(loc, UriKind.Absolute, out var uri))
                    continue;
                if (!string.Equals(uri.Host, allowedHost, StringComparison.OrdinalIgnoreCase))
                    continue;
                found.Add(UrlNormalizer.Normalize(uri));
                if (found.Count >= MaxUrls)
                    return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read sitemap {Url}", sitemapUrl);
        }
    }

    public static string ComputeContentHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
