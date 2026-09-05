using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.AvaloniaUI.App.Views.Missions;

/// <summary>Identifies the semantic role of a mission-map marker.</summary>
public enum MissionMapMarkerKind
{
    /// <summary>The planned home or launch position.</summary>
    Home,

    /// <summary>A positioned mission command.</summary>
    MissionItem
}

/// <summary>Describes one UI-neutral marker on a mission map.</summary>
/// <param name="Label">Marker label.</param>
/// <param name="Position">Geographic position.</param>
/// <param name="Kind">Semantic marker kind.</param>
public sealed record MissionMapMarker(string Label, GeoPosition Position, MissionMapMarkerKind Kind);

/// <summary>Contains the UI-neutral presentation state required to render a mission map.</summary>
/// <param name="Markers">Ordered map markers.</param>
/// <param name="Route">Ordered route positions.</param>
/// <param name="Bounds">Padded geographic bounds for fitting the mission.</param>
public sealed record MissionMapSnapshot(IReadOnlyList<MissionMapMarker> Markers, IReadOnlyList<GeoPosition> Route, GeographicBounds? Bounds)
{
    /// <summary>Gets an empty mission-map snapshot.</summary>
    public static MissionMapSnapshot Empty { get; } = new([], [], null);

    /// <summary>Determines whether another snapshot contains the same presentation values.</summary>
    public bool ContentEquals(MissionMapSnapshot other)
    {
        return Bounds == other.Bounds && Markers.SequenceEqual(other.Markers) && Route.SequenceEqual(other.Route);
    }
}
