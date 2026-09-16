namespace ClonerApp.Core.Models;

public sealed class CrawledPage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ETag { get; set; }
    public string? LastModified { get; set; }
    public string? ContentHash { get; set; }
    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
}
