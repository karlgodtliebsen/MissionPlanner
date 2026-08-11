using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Identifies how a mission-map action location was selected.</summary>
public enum MissionMapContextSource
{
    /// <summary>The location came from a mouse context action.</summary>
    ContextClick,
    /// <summary>The location came from a primary tap, supporting touch devices.</summary>
    Tap,
    /// <summary>The location came from an explicit coordinate-entry workflow.</summary>
    CoordinateEntry
}

/// <summary>Immutable location snapshot used by one mission-map command.</summary>
public sealed record MissionMapContext(GeoPosition Position, MissionMapContextSource Source, DateTimeOffset CapturedAt);
