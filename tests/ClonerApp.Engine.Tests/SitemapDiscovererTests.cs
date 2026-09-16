using ClonerApp.Engine.Crawling;

namespace ClonerApp.Engine.Tests;

public class SitemapDiscovererTests
{
    [Fact]
    public void ParseSitemapXml_Urlset_ReturnsLocs()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url><loc>https://example.com/a</loc></url>
              <url><loc>https://example.com/b</loc></url>
            </urlset>
            """;

        var urls = SitemapDiscoverer.ParseSitemapXml(xml);
        Assert.Equal(2, urls.Count);
        Assert.Contains("https://example.com/a", urls);
        Assert.Contains("https://example.com/b", urls);
    }

    [Fact]
    public void ParseSitemapXml_Index_ReturnsChildSitemapLocs()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <sitemap><loc>https://example.com/sitemap-posts.xml</loc></sitemap>
            </sitemapindex>
            """;

        var urls = SitemapDiscoverer.ParseSitemapXml(xml);
        Assert.Single(urls);
        Assert.Equal("https://example.com/sitemap-posts.xml", urls[0]);
    }

    [Fact]
    public void ComputeContentHash_IsStable()
    {
        var a = SitemapDiscoverer.ComputeContentHash("hello");
        var b = SitemapDiscoverer.ComputeContentHash("hello");
        var c = SitemapDiscoverer.ComputeContentHash("world");
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
