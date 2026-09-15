using ClonerApp.Core.Interfaces;
using ClonerApp.Core.Models;
using ClonerApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClonerApp.Infrastructure.Repositories;

public sealed class RunRepository : IRunRepository
{
    private readonly ClonerDbContext _db;

    public RunRepository(ClonerDbContext db) => _db = db;

    public async Task<CrawlRun> CreateAsync(CrawlRun run, CancellationToken cancellationToken = default)
    {
        _db.Runs.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        return run;
    }

    public async Task UpdateAsync(CrawlRun run, CancellationToken cancellationToken = default)
    {
        _db.Runs.Update(run);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CrawlRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Runs.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<CrawlRun?> GetLatestForProjectAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        await _db.Runs.AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
}
