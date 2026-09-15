using ClonerApp.Core.Interfaces;
using ClonerApp.Core.Models;
using ClonerApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ClonerApp.Infrastructure.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly ClonerDbContext _db;

    public ProjectRepository(ClonerDbContext db) => _db = db;

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _db.Projects.AsNoTracking().OrderByDescending(p => p.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<Project> AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        _db.Projects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        project.UpdatedAtUtc = DateTime.UtcNow;
        _db.Projects.Update(project);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null) return;
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Project>> GetDueProjectsAsync(DateTime utcNow, CancellationToken cancellationToken = default) =>
        await _db.Projects.AsNoTracking()
            .Where(p => p.IsEnabled && p.RunMode != Core.Enums.RunMode.Once && p.NextRunAtUtc != null && p.NextRunAtUtc <= utcNow)
            .ToListAsync(cancellationToken);
}
