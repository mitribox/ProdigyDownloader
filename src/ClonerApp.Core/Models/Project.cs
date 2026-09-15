using ClonerApp.Core.Enums;

namespace ClonerApp.Core.Models;

public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string StartUrls { get; set; } = string.Empty;
    public UrlInputMode UrlInputMode { get; set; } = UrlInputMode.Simple;
    public string? UrlPrefix { get; set; }
    public int? PageFrom { get; set; }
    public int? PageTo { get; set; }
    public string? UrlRegex { get; set; }
    public int MaxDepth { get; set; } = 2;
    public int MaxPages { get; set; } = 200;
    public bool SameDomainOnly { get; set; } = true;
    public bool HonorRobotsTxt { get; set; } = true;
    public bool ScanWithinStartingFolder { get; set; }
    public bool IgnoreHomePage { get; set; }
    public bool AlwaysScanImageLinks { get; set; } = true;
    public MediaCategory MediaCategory { get; set; } = MediaCategory.Both;
    public string SelectedExtensions { get; set; } = string.Empty;
    public long? MinFileSizeBytes { get; set; }
    public int? MinWidth { get; set; }
    public int? MinHeight { get; set; }
    public int MaxConnections { get; set; } = 4;
    public int PageDelayMs { get; set; }
    public string OutputRoot { get; set; } = string.Empty;
    public StorageLayout StorageLayout { get; set; } = StorageLayout.PageTitle;
    public RunMode RunMode { get; set; } = RunMode.Once;
    public int? MonitorIntervalMinutes { get; set; }
    public string? ScheduleTime { get; set; }
    public string? ScheduleDays { get; set; }
    public bool DeduplicateByHash { get; set; } = true;
    public bool VersionOnChange { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? NextRunAtUtc { get; set; }
    public DateTime? LastRunAtUtc { get; set; }

    public ICollection<CrawlRun> Runs { get; set; } = new List<CrawlRun>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
