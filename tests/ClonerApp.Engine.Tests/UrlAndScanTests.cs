using ClonerApp.Core.Enums;
using ClonerApp.Core.Models;
using ClonerApp.Engine.Crawling;
using ClonerApp.Engine.Storage;

namespace ClonerApp.Engine.Tests;

public class UrlSeedExpanderTests
{
    [Fact]
    public void Expand_UsesStartUrlsOnly()
    {
        var project = new Project
        {
            UrlInputMode = UrlInputMode.Advanced,
            StartUrls = "https://xyz.com/blog.php/1\nhttps://xyz.com/other",
            UrlRegex = @"blog\.php",
            UrlPrefix = "https://ignored.com/",
            PageFrom = 1,
            PageTo = 99
        };

        var urls = UrlSeedExpander.Expand(project);
        Assert.Equal(2, urls.Count);
        Assert.Contains(urls, u => u.Contains("blog.php"));
        Assert.DoesNotContain(urls, u => u.Contains("ignored.com"));
    }

    [Fact]
    public void ExpandSimple_ParsesMultipleUrls()
    {
        var urls = UrlSeedExpander.ExpandSimple("https://a.com/1\nhttps://b.com/2");
        Assert.Equal(2, urls.Count);
    }
}

public class CrawlScopeHelperTests
{
    [Fact]
    public void GetDirectoryPrefix_UsesParentForFileSeed()
    {
        var prefix = CrawlScopeHelper.GetDirectoryPrefix("https://site.com/blog/post/page.html");
        Assert.Equal("/blog/post/", prefix);
    }

    [Fact]
    public void IsWithinStartingFolder_AllowsDescendants()
    {
        var seeds = new[] { "https://site.com/blog/section/" };
        Assert.True(CrawlScopeHelper.IsWithinStartingFolder("https://site.com/blog/section/page2", seeds));
        Assert.False(CrawlScopeHelper.IsWithinStartingFolder("https://site.com/other/page", seeds));
    }

    [Fact]
    public void IsHomePage_DetectsIndex()
    {
        Assert.True(CrawlScopeHelper.IsHomePage("https://site.com/"));
        Assert.True(CrawlScopeHelper.IsHomePage("https://site.com/index.html"));
        Assert.False(CrawlScopeHelper.IsHomePage("https://site.com/blog/1"));
    }
}

public class PathBuilderFlatTests
{
    [Fact]
    public void BuildPath_Flat_UsesOutputRootDirectly()
    {
        var root = Path.Combine(Path.GetTempPath(), "pd-flat-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new Project { OutputRoot = root, StorageLayout = StorageLayout.Flat };
            var candidate = new MediaCandidate
            {
                Url = "https://example.com/gallery/deep/photo.jpg",
                SourcePageUrl = "https://example.com/page",
                SourcePageTitle = "Hello World",
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

    [Fact]
    public void BuildPath_PageTitle_CreatesSubfolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "pd-title-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new Project { OutputRoot = root, StorageLayout = StorageLayout.PageTitle };
            var candidate = new MediaCandidate
            {
                Url = "https://example.com/gallery/deep/photo.jpg",
                SourcePageUrl = "https://example.com/page",
                SourcePageTitle = "Hello World",
                Extension = "jpg",
                IsImage = true
            };

            var path = new PathBuilder().BuildPath(project, candidate);
            Assert.Equal(Path.Combine(root, "Hello World", "photo.jpg"), path);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
