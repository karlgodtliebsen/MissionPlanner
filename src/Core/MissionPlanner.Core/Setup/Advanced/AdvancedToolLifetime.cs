namespace MissionPlanner.Core.Setup.Advanced;

/// <summary>Owns one tool activation's cancellation and cleanup, never shared application services.</summary>
public sealed class AdvancedToolLifetime : IAsyncDisposable
{
    private readonly object sync = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly List<Func<ValueTask>> cleanup = [];
    private Task? disposal;

    /// <summary>Gets the cancellation token for work started by this activation.</summary>
    public CancellationToken Token { get; }

    /// <summary>Creates a fresh activation lifetime.</summary>
    public AdvancedToolLifetime()
    {
        Token = cancellation.Token;
    }

    /// <summary>Registers an owned subscription for reverse-order cleanup.</summary>
    public void Own(IDisposable subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        OnClosing(() =>
        {
            subscription.Dispose();
            return ValueTask.CompletedTask;
        });
    }

    /// <summary>Requests cancellation while activation is still registering its cleanup.</summary>
    public void Cancel()
    {
        lock (sync)
        {
            if (disposal is null)
            {
                cancellation.Cancel();
            }
        }
    }

    /// <summary>Registers cleanup for owned sinks, operation tasks, and transient secrets.</summary>
    public void OnClosing(Func<ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposal is not null, this);
            cleanup.Add(action);
        }
    }

    /// <summary>Cancels work first, then runs every cleanup even if an earlier cleanup fails.</summary>
    public ValueTask DisposeAsync()
    {
        lock (sync)
        {
            if (disposal is null)
            {
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                disposal = completion.Task;
                _ = CloseAsync(completion);
            }
            return new ValueTask(disposal);
        }
    }

    private async Task CloseAsync(TaskCompletionSource completion)
    {
        var failures = new List<Exception>();
        try
        {
            try
            {
                cancellation.Cancel();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
            for (var index = cleanup.Count - 1; index >= 0; index--)
            {
                try
                {
                    await cleanup[index]().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }
        }
        finally
        {
            cleanup.Clear();
            cancellation.Dispose();
            if (failures.Count == 0)
            {
                completion.SetResult();
            }
            else
            {
                completion.SetException(new AggregateException("Advanced tool cleanup failed.", failures));
            }
        }
    }
}
