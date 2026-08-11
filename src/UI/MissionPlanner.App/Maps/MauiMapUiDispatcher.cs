using Microsoft.Maui.Dispatching;

namespace MissionPlanner.App.Maps;

/// <summary>Adapts a MAUI dispatcher for map-layer commits.</summary>
public sealed class MauiMapUiDispatcher(IDispatcher dispatcher) : IMapUiDispatcher
{
    /// <inheritdoc />
    public ValueTask InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!dispatcher.IsDispatchRequired)
        {
            action();
            return ValueTask.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        if (!dispatcher.Dispatch(() =>
            {
                if (completion.Task.IsCompleted)
                    return;
                try { action(); completion.TrySetResult(); }
                catch (Exception exception) { completion.TrySetException(exception); }
            }))
            completion.TrySetException(new InvalidOperationException("The map UI dispatcher rejected the operation."));
        return new ValueTask(completion.Task);
    }
}
