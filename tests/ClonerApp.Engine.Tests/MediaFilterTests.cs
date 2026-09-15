using ClonerApp.Core.Enums;
using ClonerApp.Core.Models;
using ClonerApp.Engine;
using ClonerApp.Engine.Filtering;
using ClonerApp.Engine.Storage;

namespace ClonerApp.Engine.Tests;

public class MediaFilterTests
{
    private readonly MediaFilter _filter = new();

    [Fact]
    public void PassesSizeFilter_WhenUnset_AlwaysTrue()
    {
        var project = new Project();
        Assert.True(_filter.PassesSizeFilter(10, project));
        Assert.True(_filter.PassesSizeFilter(null, project));
    }

    [Fact]
    public void PassesSizeFilter_EnforcesMinimum()
    {
        var project = new Project { MinFileSizeBytes = 1000 };
        Assert.False(_filter.PassesSizeFilter(500, project));
        Assert.True(_filter.PassesSizeFilter(1000, project));
    }

    [Fact]
    public void PassesDimensionFilter_EitherAxisQualifies()
    {
        var project = new Project { MinWidth = 800, MinHeight = 600 };
        Assert.True(_filter.PassesDimensionFilter(900, 100, project));
        Assert.True(_filter.PassesDimensionFilter(100, 700, project));
        Assert.False(_filter.PassesDimensionFilter(100, 100, project));
    }

    [Fact]
    public void FilterByExtension_RespectsSelection()
    {
        var project = new Project
        {
            MediaCategory = MediaCategory.Photos,
            SelectedExtensions = "bmp,png"
        };
        var candidates = new[]
        {
            new MediaCandidate { Url = "https://a/x.bmp", SourcePageUrl = "https://a", Extension = "bmp", IsImage = true },
            new MediaCandidate { Url = "https://a/x.jpg", SourcePageUrl = "https://a", Extension = "jpg", IsImage = true },
            new MediaCandidate { Url = "https://a/x.mp4", SourcePageUrl = "https://a", Extension = "mp4", IsVideo = true }
        };

        var result = _filter.FilterByExtension(candidates, project);
        Assert.Single(result);
        Assert.Equal("bmp", result[0].Extension);
    }
}

public class ScheduleCalculatorTests
{
    [Fact]
    public void Monitor_AddsInterval()
    {
        var project = new Project { RunMode = RunMode.Monitor, MonitorIntervalMinutes = 15 };
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var next = ScheduleCalculator.CalculateNextRun(project, now);
        Assert.Equal(now.AddMinutes(15), next);
    }

    [Fact]
    public void Once_ReturnsNull()
    {
        var project = new Project { RunMode = RunMode.Once };
        Assert.Null(ScheduleCalculator.CalculateNextRun(project, DateTime.UtcNow));
    }
}

public class PathBuilderTests
{
    [Fact]
    public void Flat_UsesRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "cloner-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new Project { OutputRoot = root, StorageLayout = StorageLayout.Flat };
            var candidate = new MediaCandidate
            {
                Url = "https://example.com/gallery/photo.jpg",
                SourcePageUrl = "https://example.com/page",
                SourcePageTitle = "Hello",
                Extension = "jpg",
                IsImage = true
            };
            var path = new PathBuilder().BuildPath(project, candidate);
            Assert.Equal(Path.Combine(root, "photo.jpg"), path);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
