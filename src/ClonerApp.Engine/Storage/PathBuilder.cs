using System.Text.RegularExpressions;
using ClonerApp.Core.Enums;
using ClonerApp.Core.Models;

namespace ClonerApp.Engine.Storage;

public sealed class PathBuilder
{
    private static readonly Regex InvalidChars = new(@"[<>:""|?*\x00-\x1F]+", RegexOptions.Compiled);
    private const int MaxPathLength = 240;

    public string BuildPath(Project project, MediaCandidate candidate, string? contentHashSuffix = null)
    {
        Directory.CreateDirectory(project.OutputRoot);
        var fileName = BuildFileName(candidate.Url, contentHashSuffix);

        var directory = project.StorageLayout == StorageLayout.PageTitle
            ? Path.Combine(project.OutputRoot, Sanitize(candidate.SourcePageTitle ?? "untitled"))
            : project.OutputRoot;

        Directory.CreateDirectory(directory);
        var fullPath = Path.Combine(directory, fileName);
        return TruncatePath(fullPath, fileName);
    }

    public string BuildVersionedPath(string existingPath)
    {
        var dir = Path.GetDirectoryName(existingPath) ?? ".";
        var name = Path.GetFileNameWithoutExtension(existingPath);
        var ext = Path.GetExtension(existingPath);
        var stamped = $"{name}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
        return Path.Combine(dir, stamped);
    }

    private static string BuildFileName(string url, string? suffix)
    {
        var uri = new Uri(url);
        var name = Path.GetFileName(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(name))
            name = "download";

        name = Sanitize(Uri.UnescapeDataString(name));
        if (!string.IsNullOrEmpty(suffix))
        {
            var ext = Path.GetExtension(name);
            var stem = Path.GetFileNameWithoutExtension(name);
            name = $"{stem}_{suffix[..Math.Min(8, suffix.Length)]}{ext}";
        }

        return name;
    }

    private static string Sanitize(string value)
    {
        var cleaned = InvalidChars.Replace(value, "_").Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "item";
        return cleaned.Length > 80 ? cleaned[..80] : cleaned;
    }

    private static string TruncatePath(string fullPath, string fileName)
    {
        if (fullPath.Length <= MaxPathLength)
            return fullPath;

        var dir = Path.GetDirectoryName(fullPath) ?? ".";
        var ext = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var available = MaxPathLength - dir.Length - ext.Length - 1;
        if (available < 8)
            return Path.Combine(dir, $"f{Guid.NewGuid():N}"[..12] + ext);

        stem = stem[..Math.Min(stem.Length, available)];
        return Path.Combine(dir, stem + ext);
    }
}
