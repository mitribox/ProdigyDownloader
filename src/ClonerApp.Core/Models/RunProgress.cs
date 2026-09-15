namespace ClonerApp.Core.Models;

public sealed class RunProgress
{
    public Guid ProjectId { get; init; }
    public Guid RunId { get; init; }
    public string Message { get; init; } = string.Empty;
    public int PagesCrawled { get; init; }
    public int MediaFound { get; init; }
    public int Downloaded { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public int Filtered { get; init; }
    public bool IsCompleted { get; init; }
    public bool IsFailed { get; init; }
}
