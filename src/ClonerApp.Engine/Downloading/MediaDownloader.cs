using System.Security.Cryptography;
using ClonerApp.Core;
using ClonerApp.Core.Enums;
using ClonerApp.Core.Interfaces;
using ClonerApp.Core.Models;
using ClonerApp.Engine.Filtering;
using ClonerApp.Engine.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClonerApp.Engine.Downloading;

public sealed class MediaDownloader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MediaFilter _filter;
    private readonly PathBuilder _pathBuilder;
    private readonly ILogger<MediaDownloader> _logger;

    public MediaDownloader(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        MediaFilter filter,
        PathBuilder pathBuilder,
        ILogger<MediaDownloader> logger)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _filter = filter;
        _pathBuilder = pathBuilder;
        _logger = logger;
    }

    public async Task<DownloadOutcome> DownloadAsync(
        Project project,
        MediaCandidate candidate,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var assets = scope.ServiceProvider.GetRequiredService<IAssetRepository>();
        var existing = await assets.GetByUrlAsync(project.Id, candidate.Url, cancellationToken);
        var client = _httpClientFactory.CreateClient("cloner");

        string? etag = null;
        string? lastModified = null;
        long? contentLength = null;

        try
        {
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, candidate.Url);
            using var headResponse = await client.SendAsync(headRequest, cancellationToken);
            if (headResponse.IsSuccessStatusCode)
            {
                etag = headResponse.Headers.ETag?.Tag;
                lastModified = headResponse.Content.Headers.LastModified?.ToString("R");
                contentLength = headResponse.Content.Headers.ContentLength;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "HEAD failed for {Url}, continuing with GET", candidate.Url);
        }

        if (existing is { Status: AssetStatus.Downloaded })
        {
            var unchanged =
                (!string.IsNullOrEmpty(etag) && string.Equals(existing.ETag, etag, StringComparison.Ordinal)) ||
                (!string.IsNullOrEmpty(lastModified) && string.Equals(existing.LastModified, lastModified, StringComparison.Ordinal));

            if (unchanged || (string.IsNullOrEmpty(etag) && string.IsNullOrEmpty(lastModified)))
                return DownloadOutcome.Skipped("Already archived");
        }

        if (!_filter.PassesSizeFilter(contentLength, project))
        {
            await SaveFilteredAsync(assets, project, candidate, contentLength, etag, lastModified, "Below minimum file size", cancellationToken);
            return DownloadOutcome.Filtered("Below minimum file size");
        }

        var targetPath = _pathBuilder.BuildPath(project, candidate);
        if (existing is { Status: AssetStatus.Downloaded } && project.VersionOnChange && !string.IsNullOrEmpty(existing.LocalPath))
            targetPath = _pathBuilder.BuildVersionedPath(existing.LocalPath);

        var partialPath = targetPath + ".partial";
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        try
        {
            using var response = await client.GetAsync(candidate.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            etag ??= response.Headers.ETag?.Tag;
            lastModified ??= response.Content.Headers.LastModified?.ToString("R");
            contentLength ??= response.Content.Headers.ContentLength;

            if (!_filter.PassesSizeFilter(contentLength, project))
            {
                await SaveFilteredAsync(assets, project, candidate, contentLength, etag, lastModified, "Below minimum file size", cancellationToken);
                return DownloadOutcome.Filtered("Below minimum file size");
            }

            await using (var remote = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var local = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await remote.CopyToAsync(local, cancellationToken);
            }

            var fileInfo = new FileInfo(partialPath);
            if (!_filter.PassesSizeFilter(fileInfo.Length, project))
            {
                File.Delete(partialPath);
                await SaveFilteredAsync(assets, project, candidate, fileInfo.Length, etag, lastModified, "Below minimum file size", cancellationToken);
                return DownloadOutcome.Filtered("Below minimum file size");
            }

            int? width = null;
            int? height = null;
            if (candidate.IsImage || MediaExtensions.IsImageExtension(candidate.Extension))
            {
                var dims = ImageDimensionReader.TryRead(partialPath);
                if (dims is not null)
                {
                    width = dims.Value.Width;
                    height = dims.Value.Height;
                }

                if (!_filter.PassesDimensionFilter(width, height, project))
                {
                    File.Delete(partialPath);
                    await SaveFilteredAsync(assets, project, candidate, fileInfo.Length, etag, lastModified, "Below minimum dimensions", cancellationToken, width, height);
                    return DownloadOutcome.Filtered("Below minimum dimensions");
                }
            }

            var hash = await ComputeSha256Async(partialPath, cancellationToken);
            if (project.DeduplicateByHash && await assets.ExistsByHashAsync(project.Id, hash, cancellationToken))
            {
                if (existing is null || !string.Equals(existing.ContentHash, hash, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(partialPath);
                    await UpsertAssetAsync(assets, project, candidate, null, hash, etag, lastModified, fileInfo.Length, width, height, AssetStatus.Skipped, "Duplicate content hash", cancellationToken);
                    return DownloadOutcome.Skipped("Duplicate content hash");
                }
            }

            if (File.Exists(targetPath))
                targetPath = _pathBuilder.BuildPath(project, candidate, hash);

            if (File.Exists(targetPath))
                File.Delete(targetPath);
            File.Move(partialPath, targetPath);

            await UpsertAssetAsync(assets, project, candidate, targetPath, hash, etag, lastModified, new FileInfo(targetPath).Length, width, height, AssetStatus.Downloaded, null, cancellationToken);
            return DownloadOutcome.Downloaded(targetPath);
        }
        catch (OperationCanceledException)
        {
            CleanupPartial(partialPath);
            throw;
        }
        catch (Exception ex)
        {
            CleanupPartial(partialPath);
            _logger.LogWarning(ex, "Download failed for {Url}", candidate.Url);
            await UpsertAssetAsync(assets, project, candidate, null, null, etag, lastModified, contentLength, null, null, AssetStatus.Failed, ex.Message, cancellationToken);
            return DownloadOutcome.Failed(ex.Message);
        }
    }

    private static Task SaveFilteredAsync(
        IAssetRepository assets,
        Project project,
        MediaCandidate candidate,
        long? size,
        string? etag,
        string? lastModified,
        string reason,
        CancellationToken cancellationToken,
        int? width = null,
        int? height = null) =>
        UpsertAssetAsync(assets, project, candidate, null, null, etag, lastModified, size, width, height, AssetStatus.Filtered, reason, cancellationToken);

    private static async Task UpsertAssetAsync(
        IAssetRepository assets,
        Project project,
        MediaCandidate candidate,
        string? localPath,
        string? hash,
        string? etag,
        string? lastModified,
        long? size,
        int? width,
        int? height,
        AssetStatus status,
        string? error,
        CancellationToken cancellationToken)
    {
        await assets.UpsertAsync(new Asset
        {
            ProjectId = project.Id,
            NormalizedUrl = candidate.Url,
            SourcePageUrl = candidate.SourcePageUrl,
            SourcePageTitle = candidate.SourcePageTitle,
            LocalPath = localPath,
            ContentHash = hash,
            ETag = etag,
            LastModified = lastModified,
            SizeBytes = size,
            Width = width,
            Height = height,
            Extension = candidate.Extension,
            Status = status,
            ErrorMessage = error,
            DownloadedAtUtc = status == AssetStatus.Downloaded ? DateTime.UtcNow : null
        }, cancellationToken);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void CleanupPartial(string partialPath)
    {
        try
        {
            if (File.Exists(partialPath))
                File.Delete(partialPath);
        }
        catch
        {
            // ignore
        }
    }
}

public sealed class DownloadOutcome
{
    public AssetStatus Status { get; init; }
    public string? Path { get; init; }
    public string? Message { get; init; }

    public static DownloadOutcome Downloaded(string path) => new() { Status = AssetStatus.Downloaded, Path = path };
    public static DownloadOutcome Skipped(string message) => new() { Status = AssetStatus.Skipped, Message = message };
    public static DownloadOutcome Filtered(string message) => new() { Status = AssetStatus.Filtered, Message = message };
    public static DownloadOutcome Failed(string message) => new() { Status = AssetStatus.Failed, Message = message };
}
