using ClonerApp.Engine.Crawling;

namespace ClonerApp.Engine.Tests;

public class HtmlMediaExtractorTests
{
    [Fact]
    public void Extract_FindsImagesAndLinks()
    {
        var html = """
            <html><head><title>Demo</title></head>
            <body>
              <img src="/a.png" />
              <a href="/page2.html">next</a>
              <a href="/clip.mp4">video</a>
            </body></html>
            """;

        var result = new HtmlMediaExtractor().Extract(html, new Uri("https://example.com/index.html"));
        Assert.Equal("Demo", result.Title);
        Assert.Contains(result.Media, m => m.Url.EndsWith("/a.png"));
        Assert.Contains(result.Media, m => m.Url.EndsWith("/clip.mp4"));
        Assert.Contains(result.PageLinks, l => l.Contains("page2.html"));
        Assert.Equal("img", result.Media.First(m => m.Url.EndsWith("/a.png")).SourceTag);
        Assert.Equal("a", result.Media.First(m => m.Url.EndsWith("/clip.mp4")).SourceTag);
    }

    [Fact]
    public void Extract_WhenAlwaysScanImageLinksFalse_SkipsAnchorMedia()
    {
        var html = """
            <html><body>
              <img src="/a.png" />
              <a href="/clip.mp4">video</a>
            </body></html>
            """;

        var result = new HtmlMediaExtractor().Extract(html, new Uri("https://example.com/index.html"), alwaysScanImageLinks: false);
        Assert.Contains(result.Media, m => m.Url.EndsWith("/a.png"));
        Assert.DoesNotContain(result.Media, m => m.Url.EndsWith("/clip.mp4"));
    }
}
