using ClonerApp.Core.Models;

namespace ClonerApp.Core.Interfaces;

public interface IRunRepository
{
    Task<CrawlRun> CreateAsync(CrawlRun run, CancellationToken cancellationToken = default);
    Task UpdateAsync(CrawlRun run, CancellationToken cancellationToken = default);
    Task<CrawlRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CrawlRun?> GetLatestForProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
