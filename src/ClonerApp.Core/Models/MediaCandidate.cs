namespace ClonerApp.Core.Models;

public sealed class MediaCandidate
{
    public required string Url { get; init; }
    public required string SourcePageUrl { get; init; }
    public string? SourcePageTitle { get; init; }
    public string? SourceTag { get; init; }
    public string? Extension { get; init; }
    public bool IsImage { get; init; }
    public bool IsVideo { get; init; }
}
