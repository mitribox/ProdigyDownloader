using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using ClonerApp.Core;
using ClonerApp.Core.Models;

namespace ClonerApp.Engine.Crawling;

public sealed class HtmlMediaExtractor
{
    private static readonly Regex CssUrlRegex = new(
        @"url\(\s*['""]?(?<url>[^'"")\s]+)['""]?\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HtmlParser _parser = new();

    public ExtractedPage Extract(string html, Uri pageUrl, bool alwaysScanImageLinks = true)
    {
        var document = _parser.ParseDocument(html);
        var title = document.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = pageUrl.AbsolutePath.Trim('/') is { Length: > 0 } p ? p : pageUrl.Host;

        var media = new List<MediaCandidate>();
        var links = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var img in document.QuerySelectorAll("img[src], img[data-src], img[data-lazy-src]"))
        {
            AddMedia(media, pageUrl, title, GetAttr(img, "src") ?? GetAttr(img, "data-src") ?? GetAttr(img, "data-lazy-src"));
            ParseSrcSet(media, pageUrl, title, GetAttr(img, "srcset"));
        }

        foreach (var source in document.QuerySelectorAll("source[src], source[srcset], video[src], video source[src], audio[src]"))
        {
            AddMedia(media, pageUrl, title, GetAttr(source, "src"));
            ParseSrcSet(media, pageUrl, title, GetAttr(source, "srcset"));
        }

        foreach (var anchor in document.QuerySelectorAll("a[href]"))
        {
            var href = GetAttr(anchor, "href");
            if (string.IsNullOrWhiteSpace(href)) continue;

            var absolute = UrlNormalizer.TryResolve(pageUrl, href);
            if (absolute is null) continue;

            var ext = MediaExtensions.GetExtension(absolute.AbsoluteUri);
            if (MediaExtensions.IsImageExtension(ext) || MediaExtensions.IsVideoExtension(ext))
            {
                if (alwaysScanImageLinks)
                    AddMedia(media, pageUrl, title, absolute.AbsoluteUri);
            }
            else if (LooksLikeHtmlPage(absolute))
            {
                links.Add(UrlNormalizer.Normalize(absolute));
            }
        }

        foreach (var style in document.QuerySelectorAll("[style]"))
        {
            var styleValue = GetAttr(style, "style");
            if (string.IsNullOrWhiteSpace(styleValue)) continue;
            foreach (Match match in CssUrlRegex.Matches(styleValue))
                AddMedia(media, pageUrl, title, match.Groups["url"].Value);
        }

        foreach (var styleTag in document.QuerySelectorAll("style"))
        {
            var css = styleTag.TextContent;
            if (string.IsNullOrWhiteSpace(css)) continue;
            foreach (Match match in CssUrlRegex.Matches(css))
                AddMedia(media, pageUrl, title, match.Groups["url"].Value);
        }

        return new ExtractedPage(title, media, links.ToList());
    }

    private static void ParseSrcSet(List<MediaCandidate> media, Uri pageUrl, string? title, string? srcset)
    {
        if (string.IsNullOrWhiteSpace(srcset)) return;
        foreach (var part in srcset.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var urlPart = part.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            AddMedia(media, pageUrl, title, urlPart);
        }
    }

    private static void AddMedia(List<MediaCandidate> media, Uri pageUrl, string? title, string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return;
        if (rawUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return;

        var absolute = UrlNormalizer.TryResolve(pageUrl, rawUrl);
        if (absolute is null) return;

        var normalized = UrlNormalizer.Normalize(absolute);
        var ext = MediaExtensions.GetExtension(normalized);
        var isImage = MediaExtensions.IsImageExtension(ext);
        var isVideo = MediaExtensions.IsVideoExtension(ext);
        if (!isImage && !isVideo) return;

        if (media.Any(m => string.Equals(m.Url, normalized, StringComparison.OrdinalIgnoreCase)))
            return;

        media.Add(new MediaCandidate
        {
            Url = normalized,
            SourcePageUrl = UrlNormalizer.Normalize(pageUrl),
            SourcePageTitle = title,
            Extension = ext,
            IsImage = isImage,
            IsVideo = isVideo
        });
    }

    private static string? GetAttr(AngleSharp.Dom.IElement element, string name) =>
        element.GetAttribute(name)?.Trim();

    private static bool LooksLikeHtmlPage(Uri uri)
    {
        var ext = MediaExtensions.GetExtension(uri.AbsoluteUri);
        if (string.IsNullOrEmpty(ext)) return true;
        return ext is "html" or "htm" or "php" or "asp" or "aspx" or "jsp";
    }
}

public sealed record ExtractedPage(
    string? Title,
    IReadOnlyList<MediaCandidate> Media,
    IReadOnlyList<string> PageLinks);
