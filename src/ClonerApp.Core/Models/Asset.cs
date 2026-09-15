using ClonerApp.Core.Enums;

namespace ClonerApp.Core.Models;

public sealed class Asset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public string NormalizedUrl { get; set; } = string.Empty;
    public string? SourcePageUrl { get; set; }
    public string? SourcePageTitle { get; set; }
    public string? LocalPath { get; set; }
    public string? ContentHash { get; set; }
    public string? ETag { get; set; }
    public string? LastModified { get; set; }
    public long? SizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Extension { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime FirstSeenAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DownloadedAtUtc { get; set; }
}
