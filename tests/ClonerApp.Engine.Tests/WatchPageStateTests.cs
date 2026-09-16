using ClonerApp.Core.Models;
using ClonerApp.Engine.Crawling;

namespace ClonerApp.Engine.Tests;

public class WatchPageStateTests
{
    [Fact]
    public async Task Upsert_TracksNewAndUnchangedHashes()
    {
        var repo = new InMemoryCrawledPageRepository();
        var projectId = Guid.NewGuid();
        var url = "https://example.com/post/1";
        var hash1 = SitemapDiscoverer.ComputeContentHash("<html>v1</html>");
        var hash2 = SitemapDiscoverer.ComputeContentHash("<html>v2</html>");

        await repo.UpsertAsync(new CrawledPage
        {
            ProjectId = projectId,
            Url = url,
            ContentHash = hash1,
            LastSeenAtUtc = DateTime.UtcNow
        });

        var prior = await repo.GetByUrlAsync(projectId, url);
        Assert.NotNull(prior);
        Assert.Equal(hash1, prior!.ContentHash);
        Assert.False(string.Equals(prior.ContentHash, hash2, StringComparison.OrdinalIgnoreCase));

        await repo.UpsertAsync(new CrawledPage
        {
            ProjectId = projectId,
            Url = url,
            ContentHash = hash2,
            LastSeenAtUtc = DateTime.UtcNow
        });

        var updated = await repo.GetByUrlAsync(projectId, url);
        Assert.Equal(hash2, updated!.ContentHash);
        var known = await repo.GetKnownUrlsAsync(projectId);
        Assert.Contains(url, known);
    }
}
