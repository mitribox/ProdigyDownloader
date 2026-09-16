using ClonerApp.Engine.Crawling;

namespace ClonerApp.Engine.Tests;

public class CrawlPresetsTests
{
    [Fact]
    public void ResolveScope_EntireSite_AppliesCapsAndSameDomain()
    {
        var (depth, pages, sameDomain, folder) = CrawlPresets.ResolveScope(
            crawlEntireSite: true,
            maxDepth: 2,
            maxPages: 200,
            sameDomainOnly: false,
            scanWithinStartingFolder: true);

        Assert.Equal(CrawlPresets.EntireSiteMaxDepth, depth);
        Assert.Equal(CrawlPresets.EntireSiteMaxPages, pages);
        Assert.True(sameDomain);
        Assert.False(folder);
    }

    [Fact]
    public void ResolveScope_Limited_KeepsUserValues()
    {
        var (depth, pages, sameDomain, folder) = CrawlPresets.ResolveScope(
            crawlEntireSite: false,
            maxDepth: 3,
            maxPages: 50,
            sameDomainOnly: true,
            scanWithinStartingFolder: true);

        Assert.Equal(3, depth);
        Assert.Equal(50, pages);
        Assert.True(sameDomain);
        Assert.True(folder);
    }
}
