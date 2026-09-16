using ClonerApp.Core.Interfaces;
using ClonerApp.Core.Models;
using ClonerApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClonerApp.Infrastructure.Repositories;

public sealed class CrawledPageRepository : ICrawledPageRepository
{
    private readonly ClonerDbContext _db;

    public CrawledPageRepository(ClonerDbContext db) => _db = db;

    public async Task<CrawledPage?> GetByUrlAsync(Guid projectId, string url, CancellationToken cancellationToken = default) =>
        await _db.CrawledPages.FirstOrDefaultAsync(
            p => p.ProjectId == projectId && p.Url == url,
            cancellationToken);

    public async Task UpsertAsync(CrawledPage page, CancellationToken cancellationToken = default)
    {
        var existing = await _db.CrawledPages.FirstOrDefaultAsync(
            p => p.ProjectId == page.ProjectId && p.Url == page.Url,
            cancellationToken);

        if (existing is null)
        {
            _db.CrawledPages.Add(page);
        }
        else
        {
            existing.ETag = page.ETag;
            existing.LastModified = page.LastModified;
            existing.ContentHash = page.ContentHash;
            existing.LastSeenAtUtc = page.LastSeenAtUtc;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetKnownUrlsAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        await _db.CrawledPages.AsNoTracking()
            .Where(p => p.ProjectId == projectId)
            .Select(p => p.Url)
            .ToListAsync(cancellationToken);
}
