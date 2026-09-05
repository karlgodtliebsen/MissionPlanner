using Avalonia.Threading;

namespace MissionPlanner.App.Maps;

/// <summary>Adapts the Avalonia UI dispatcher for map-layer commits.</summary>
public sealed class AvaloniaMapUiDispatcher(Dispatcher dispatcher) : IMapUiDispatcher
{
    /// <inheritdoc />
    public async ValueTask InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        await dispatcher.InvokeAsync(action, DispatcherPriority.Normal, cancellationToken);
    }
}
