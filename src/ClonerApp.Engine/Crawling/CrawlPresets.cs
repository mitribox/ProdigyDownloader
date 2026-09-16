namespace ClonerApp.Engine.Crawling;

public static class CrawlPresets
{
    public const int EntireSiteMaxDepth = 50;
    public const int EntireSiteMaxPages = 10_000;

    public static (int MaxDepth, int MaxPages, bool SameDomainOnly, bool ScanWithinStartingFolder) ResolveScope(
        bool crawlEntireSite,
        int maxDepth,
        int maxPages,
        bool sameDomainOnly,
        bool scanWithinStartingFolder)
    {
        if (!crawlEntireSite)
            return (maxDepth, maxPages, sameDomainOnly, scanWithinStartingFolder);

        return (EntireSiteMaxDepth, EntireSiteMaxPages, true, false);
    }
}
