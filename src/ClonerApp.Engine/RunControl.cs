namespace ClonerApp.Engine;

/// <summary>
/// Per-run cancellation and cooperative pause gate.
/// </summary>
internal sealed class RunControl : IDisposable
{
    private readonly CancellationTokenSource _cts;
    private readonly ManualResetEventSlim _pauseGate = new(initialState: true);
    private int _paused; // 0 = running, 1 = paused

    public RunControl(CancellationToken externalToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
    }

    public CancellationToken Token => _cts.Token;
    public bool IsPaused => Volatile.Read(ref _paused) == 1;

    public void Cancel()
    {
        Resume();
        _cts.Cancel();
    }

    public void Pause()
    {
        if (Interlocked.Exchange(ref _paused, 1) == 0)
            _pauseGate.Reset();
    }

    public void Resume()
    {
        if (Interlocked.Exchange(ref _paused, 0) == 1)
            _pauseGate.Set();
    }

    public async Task WaitIfPausedAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        while (IsPaused)
        {
            linked.Token.ThrowIfCancellationRequested();
            try
            {
                // Wake promptly on resume or cancel without blocking a thread pool thread forever.
                _pauseGate.Wait(250, linked.Token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        linked.Token.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public void Dispose()
    {
        Resume();
        _cts.Dispose();
        _pauseGate.Dispose();
    }
}
