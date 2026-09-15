using System.Text.RegularExpressions;

namespace ClonerApp.Engine.Crawling;

public static class CrawlScopeHelper
{
    private static readonly HashSet<string> HomeFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "", "index", "index.html", "index.htm", "index.php", "index.asp", "index.aspx",
        "home", "home.html", "home.htm", "home.php", "default.aspx", "default.html"
    };

    public static string GetDirectoryPrefix(string seedUrl)
    {
        var uri = new Uri(seedUrl);
        var path = uri.AbsolutePath;
        if (string.IsNullOrEmpty(path) || path == "/")
            return "/";

        // If last segment looks like a file, use its parent directory.
        var lastSlash = path.LastIndexOf('/');
        if (lastSlash <= 0)
            return "/";

        var lastSegment = path[(lastSlash + 1)..];
        if (lastSegment.Contains('.') && !lastSegment.EndsWith('/'))
            path = path[..(lastSlash + 1)];
        else if (!path.EndsWith('/'))
            path += "/";

        return path;
    }

    public static bool IsWithinStartingFolder(string candidateUrl, IEnumerable<string> seedUrls)
    {
        if (!Uri.TryCreate(candidateUrl, UriKind.Absolute, out var candidate))
            return false;

        foreach (var seed in seedUrls)
        {
            if (!Uri.TryCreate(seed, UriKind.Absolute, out var seedUri))
                continue;

            if (!string.Equals(candidate.Host, seedUri.Host, StringComparison.OrdinalIgnoreCase))
                continue;

            var prefix = GetDirectoryPrefix(seed);
            var path = candidate.AbsolutePath;
            if (!path.EndsWith('/') && path.Contains('.') && path.LastIndexOf('/') >= 0)
            {
                // Compare directory of candidate for page links
            }

            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;

            // Also allow exact seed directory without trailing file
            if (prefix != "/" && string.Equals(path.TrimEnd('/') + "/", prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool IsHomePage(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var path = uri.AbsolutePath.Trim('/');
        if (HomeFileNames.Contains(path))
            return true;

        // Single-segment home-like paths already covered; bare host with empty path
        return string.IsNullOrEmpty(path);
    }

    public static bool MatchesRegex(string url, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return true;

        try
        {
            return Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
