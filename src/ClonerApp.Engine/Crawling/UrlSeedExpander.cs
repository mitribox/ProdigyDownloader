using ClonerApp.Core.Models;

namespace ClonerApp.Engine.Crawling;

public static class UrlSeedExpander
{
    public static IReadOnlyList<string> Expand(Project project)
    {
        var seeds = ExpandSimple(project.StartUrls);

        // Seed URLs are always kept; regex mainly constrains followed links during crawl.
        // Optionally filter seeds too when Advanced mode + regex is set, except keep at least seeds that are explicit starts.
        // Plan: do not drop explicit start URLs via regex — crawler applies regex to followed links.
        return seeds
            .Select(UrlNormalizer.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> ExpandSimple(string? startUrls)
    {
        if (string.IsNullOrWhiteSpace(startUrls))
            return Array.Empty<string>();

        return startUrls
            .Split(['\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .ToList();
    }
}
