using ClonerApp.Core.Enums;

namespace ClonerApp.Core.Models;

public sealed class CrawlRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public RunStatus Status { get; set; } = RunStatus.Pending;
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public int PagesCrawled { get; set; }
    public int MediaFound { get; set; }
    public int Downloaded { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int Filtered { get; set; }
    public string? ErrorMessage { get; set; }
}
