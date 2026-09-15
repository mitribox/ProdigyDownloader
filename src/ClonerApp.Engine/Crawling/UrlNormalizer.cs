namespace ClonerApp.Engine.Crawling;

public static class UrlNormalizer
{
    public static Uri? TryResolve(Uri baseUrl, string relativeOrAbsolute)
    {
        try
        {
            if (!Uri.TryCreate(baseUrl, relativeOrAbsolute, out var uri))
                return null;
            if (uri.Scheme is not ("http" or "https"))
                return null;
            return uri;
        }
        catch
        {
            return null;
        }
    }

    public static string Normalize(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty
        };

        if ((builder.Scheme == "http" && builder.Port == 80) ||
            (builder.Scheme == "https" && builder.Port == 443))
        {
            builder.Port = -1;
        }

        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    public static string Normalize(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? Normalize(uri) : url.TrimEnd('/');
}
