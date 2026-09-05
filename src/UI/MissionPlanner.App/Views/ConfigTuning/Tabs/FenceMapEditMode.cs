namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Identifies how clicks on the dedicated fence map modify geometry.</summary>
public enum FenceMapEditMode
{
    /// <summary>Map clicks do not edit fence geometry.</summary>
    None,

    /// <summary>Map clicks append vertices to an inclusion polygon.</summary>
    PolygonInclusion,

    /// <summary>Map clicks append vertices to an exclusion polygon.</summary>
    PolygonExclusion,

    /// <summary>The next map click creates an inclusion circle.</summary>
    CircleInclusion,

    /// <summary>The next map click creates an exclusion circle.</summary>
    CircleExclusion,

    /// <summary>The next map click sets the legacy fence return point.</summary>
    ReturnPoint
}

