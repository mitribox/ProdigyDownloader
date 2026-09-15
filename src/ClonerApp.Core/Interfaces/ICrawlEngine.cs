using ClonerApp.Core.Models;

namespace ClonerApp.Core.Interfaces;

public interface ICrawlEngine
{
    event EventHandler<RunProgress>? ProgressChanged;
    Task RunProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    bool IsRunning(Guid projectId);
    void Cancel(Guid projectId);
}
