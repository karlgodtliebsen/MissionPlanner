namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Identifies the mutually exclusive planning interaction active on a mission map.</summary>
public enum MissionMapInteractionMode
{
    /// <summary>No temporary map interaction is active.</summary>
    None,
    /// <summary>Map clicks append vertices to a planning polygon.</summary>
    DrawPolygon,
    /// <summary>Map clicks define a temporary distance measurement.</summary>
    MeasureDistance,
    /// <summary>The next map click selects a fence return location.</summary>
    SetFenceReturnLocation,
    /// <summary>The next map click creates a rally point.</summary>
    SetRallyPoint,
    /// <summary>The next map click creates a point of interest.</summary>
    AddPoi,
    /// <summary>The next map click sets antenna-tracker home.</summary>
    SetTrackerHome
}
