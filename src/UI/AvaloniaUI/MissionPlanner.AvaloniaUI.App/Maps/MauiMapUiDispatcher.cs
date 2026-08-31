using Avalonia.Threading;

namespace MissionPlanner.AvaloniaUI.App.Maps;

/// <summary>Adapts a MAUI Dispatcher for map-layer commits.</summary>
public sealed class MauiMapUiDispatcher(IDispatcher dispatcher) : IMapUiDispatcher
{
    /// <inheritdoc />
    public async ValueTask InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        dispatcher.Post(() => action());

        await Dispatcher.UIThread.InvokeAsync(() => action());

        //Avalonia.AvaloniaObject


        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        dispatcher.Post(() =>
            {
                if (completion.Task.IsCompleted)
                {
                    return;
                }

                try
                {
                    action();
                    completion.TrySetResult();
                }
                catch (Exception exception) { completion.TrySetException(exception); }
            });
        completion.TrySetException(new InvalidOperationException("The map UI Dispatcher rejected the operation."));
    }
}
