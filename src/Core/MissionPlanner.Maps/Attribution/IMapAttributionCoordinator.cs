using MissionPlanner.Maps.Sources;

namespace MissionPlanner.Maps.Attribution;

/// <summary>Coordinates visible basemap attribution independently of a UI framework.</summary>
public interface IMapAttributionCoordinator
{
    /// <summary>Gets the current display and export state.</summary>
    MapAttributionOverlayState Current { get; }

    /// <summary>Raised when current attribution changes.</summary>
    event EventHandler<MapAttributionOverlayState>? Changed;

    /// <summary>Tracks a committed resolved basemap.</summary>
    ValueTask SetBasemapAsync(ResolvedMapSource? source, CancellationToken cancellationToken = default);

    /// <summary>Refreshes dynamic metadata for the current basemap.</summary>
    ValueTask RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>Toggles compact and expanded presentation.</summary>
    void ToggleExpanded();
}
