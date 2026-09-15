using ClonerApp.Core.Models;

namespace ClonerApp.Core.Interfaces;

public interface IAssetRepository
{
    Task<Asset?> GetByUrlAsync(Guid projectId, string normalizedUrl, CancellationToken cancellationToken = default);
    Task<bool> ExistsByHashAsync(Guid projectId, string contentHash, CancellationToken cancellationToken = default);
    Task UpsertAsync(Asset asset, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Asset>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
