using ClonerApp.Core.Interfaces;
using ClonerApp.Core.Models;

namespace ClonerApp.Engine.Tests;

public class InMemoryCrawledPageRepository : ICrawledPageRepository
{
    private readonly Dictionary<(Guid, string), CrawledPage> _pages = new();

    public Task<CrawledPage?> GetByUrlAsync(Guid projectId, string url, CancellationToken cancellationToken = default)
    {
        _pages.TryGetValue((projectId, url), out var page);
        return Task.FromResult(page);
    }

    public Task UpsertAsync(CrawledPage page, CancellationToken cancellationToken = default)
    {
        _pages[(page.ProjectId, page.Url)] = page;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetKnownUrlsAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(
            _pages.Where(kv => kv.Key.Item1 == projectId).Select(kv => kv.Key.Item2).ToList());
}
