using ClonerApp.Core;
using ClonerApp.Core.Enums;
using ClonerApp.Core.Models;

namespace ClonerApp.Engine.Filtering;

public sealed class MediaFilter
{
    public IReadOnlyList<MediaCandidate> FilterByExtension(IEnumerable<MediaCandidate> candidates, Project project)
    {
        var allowed = ParseExtensions(project);
        return candidates
            .Where(c =>
            {
                if (string.IsNullOrEmpty(c.Extension)) return false;
                if (allowed.Count > 0 && !allowed.Contains(c.Extension)) return false;

                return project.MediaCategory switch
                {
                    MediaCategory.Photos => c.IsImage,
                    MediaCategory.Videos => c.IsVideo,
                    _ => c.IsImage || c.IsVideo
                };
            })
            .ToList();
    }

    public bool PassesSizeFilter(long? sizeBytes, Project project)
    {
        if (project.MinFileSizeBytes is null || project.MinFileSizeBytes <= 0) return true;
        if (sizeBytes is null) return true;
        return sizeBytes.Value >= project.MinFileSizeBytes.Value;
    }

    public bool PassesDimensionFilter(int? width, int? height, Project project)
    {
        var minW = project.MinWidth;
        var minH = project.MinHeight;
        var hasMinW = minW is > 0;
        var hasMinH = minH is > 0;
        if (!hasMinW && !hasMinH)
            return true;

        // Either axis can qualify when that minimum is set.
        var widthOk = hasMinW && width.HasValue && width.Value >= minW!.Value;
        var heightOk = hasMinH && height.HasValue && height.Value >= minH!.Value;

        if (hasMinW && hasMinH)
            return widthOk || heightOk;
        if (hasMinW)
            return widthOk;
        return heightOk;
    }

    private static HashSet<string> ParseExtensions(Project project)
    {
        if (string.IsNullOrWhiteSpace(project.SelectedExtensions))
            return MediaExtensions.DefaultFor(project.MediaCategory).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return project.SelectedExtensions
            .Split([',', ';', ' ', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.TrimStart('.').ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
