namespace MissionPlanner.Core.ConfigTuning.Fences;

/// <summary>Identifies a supported fence geometry primitive.</summary>
public enum FenceAreaKind
{
    /// <summary>The vehicle must remain inside the polygon.</summary>
    PolygonInclusion,

    /// <summary>The vehicle must remain outside the polygon.</summary>
    PolygonExclusion,

    /// <summary>The vehicle must remain inside the circle.</summary>
    CircleInclusion,

    /// <summary>The vehicle must remain outside the circle.</summary>
    CircleExclusion
}
