using ClonerApp.Core.Models;

namespace ClonerApp.Core.Interfaces;

public interface ICrawledPageRepository
{
    Task<CrawledPage?> GetByUrlAsync(Guid projectId, string url, CancellationToken cancellationToken = default);
    Task UpsertAsync(CrawledPage page, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetKnownUrlsAsync(Guid projectId, CancellationToken cancellationToken = default);
}
