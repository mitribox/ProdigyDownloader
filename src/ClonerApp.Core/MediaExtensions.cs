namespace ClonerApp.Core;

public static class MediaExtensions
{
    public static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "jpg", "jpeg", "png", "gif", "webp", "bmp", "svg", "ico", "tif", "tiff", "avif"
    };

    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "mp4", "webm", "mkv", "mov", "avi", "m4v", "mpg", "mpeg", "ogv"
    };

    public static IReadOnlyList<string> DefaultFor(Enums.MediaCategory category) =>
        category switch
        {
            Enums.MediaCategory.Photos => PhotoExtensions.OrderBy(x => x).ToList(),
            Enums.MediaCategory.Videos => VideoExtensions.OrderBy(x => x).ToList(),
            _ => PhotoExtensions.Concat(VideoExtensions).OrderBy(x => x).ToList()
        };

    public static string? GetExtension(string urlOrPath)
    {
        try
        {
            var path = urlOrPath;
            if (Uri.TryCreate(urlOrPath, UriKind.Absolute, out var uri))
                path = uri.AbsolutePath;

            var ext = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(ext))
                return null;

            return ext.TrimStart('.').ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    public static bool IsImageExtension(string? ext) =>
        !string.IsNullOrEmpty(ext) && PhotoExtensions.Contains(ext);

    public static bool IsVideoExtension(string? ext) =>
        !string.IsNullOrEmpty(ext) && VideoExtensions.Contains(ext);
}
