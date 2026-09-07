namespace MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;

/// <summary>Owns one panel's cancellable read operation across load/unload cycles. Called on the UI context.</summary>
internal sealed class FirmwarePanelLoader
{
    private CancellationTokenSource? lifetime;
    private CancellationTokenSource? operation;
    private Task pending = Task.CompletedTask;
    private long generation;

    /// <summary>Gets whether the panel is still attached.</summary>
    public bool IsActive => lifetime is not null;

    /// <summary>Joins prior cleanup before accepting a new activation.</summary>
    public async Task<bool> ActivateAsync()
    {
        if (lifetime is not null) { return false; }
        var version = ++generation;
        await pending;
        if (version != generation) { return false; }
        lifetime = new CancellationTokenSource();
        return true;
    }

    /// <summary>Coalesces duplicate requests into the single pending read.</summary>
    public Task RunAsync(Func<CancellationToken, Task> load, CancellationToken token)
    {
        if (lifetime is null) { return Task.CompletedTask; }
        if (!pending.IsCompleted) { return pending; }
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, token);
        operation = cancellation;
        pending = ExecuteAsync(load, cancellation);
        return pending;
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task> load, CancellationTokenSource cancellation)
    {
        // Publish the owned task before callbacks can request cancellation/unload.
        await Task.Yield();
        try
        {
            cancellation.Token.ThrowIfCancellationRequested();
            await load(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(operation, cancellation)) { operation = null; }
            cancellation.Dispose();
        }
    }

    /// <summary>Requests cancellation of the current read.</summary>
    public void Cancel() => operation?.Cancel();

    /// <summary>Joins the cancelled read before changing its query.</summary>
    public async Task CancelAndWaitAsync()
    {
        Cancel();
        await pending;
    }

    /// <summary>Rejects future work, cancels and joins the current read, and releases its lifetime.</summary>
    public async Task DeactivateAsync()
    {
        generation++;
        var previous = lifetime;
        lifetime = null;
        previous?.Cancel();
        try { await pending; }
        finally { previous?.Dispose(); }
    }
}
