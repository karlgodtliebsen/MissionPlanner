namespace MissionPlanner.AvaloniaUI.App.Maps;

/// <summary>Dispatches Mapsui mutations to the platform UI thread.</summary>
public interface IMapUiDispatcher
{
    /// <summary>Invokes an action on the UI thread.</summary>
    ValueTask InvokeAsync(Action action, CancellationToken cancellationToken = default);
}
