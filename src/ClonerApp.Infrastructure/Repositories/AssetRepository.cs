using ClonerApp.Core.Interfaces;
using ClonerApp.Core.Models;
using ClonerApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClonerApp.Infrastructure.Repositories;

public sealed class AssetRepository : IAssetRepository
{
    private readonly ClonerDbContext _db;

    public AssetRepository(ClonerDbContext db) => _db = db;

    public async Task<Asset?> GetByUrlAsync(Guid projectId, string normalizedUrl, CancellationToken cancellationToken = default) =>
        await _db.Assets.FirstOrDefaultAsync(a => a.ProjectId == projectId && a.NormalizedUrl == normalizedUrl, cancellationToken);

    public async Task<bool> ExistsByHashAsync(Guid projectId, string contentHash, CancellationToken cancellationToken = default) =>
        await _db.Assets.AnyAsync(a =>
            a.ProjectId == projectId &&
            a.ContentHash == contentHash &&
            a.Status == Core.Enums.AssetStatus.Downloaded,
            cancellationToken);

    public async Task UpsertAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Assets.FirstOrDefaultAsync(
            a => a.ProjectId == asset.ProjectId && a.NormalizedUrl == asset.NormalizedUrl,
            cancellationToken);

        if (existing is null)
        {
            _db.Assets.Add(asset);
        }
        else
        {
            existing.SourcePageUrl = asset.SourcePageUrl;
            existing.SourcePageTitle = asset.SourcePageTitle;
            existing.LocalPath = asset.LocalPath;
            existing.ContentHash = asset.ContentHash;
            existing.ETag = asset.ETag;
            existing.LastModified = asset.LastModified;
            existing.SizeBytes = asset.SizeBytes;
            existing.Width = asset.Width;
            existing.Height = asset.Height;
            existing.Extension = asset.Extension;
            existing.Status = asset.Status;
            existing.ErrorMessage = asset.ErrorMessage;
            existing.DownloadedAtUtc = asset.DownloadedAtUtc;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Asset>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        await _db.Assets.AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .OrderByDescending(a => a.DownloadedAtUtc)
            .ToListAsync(cancellationToken);
}
