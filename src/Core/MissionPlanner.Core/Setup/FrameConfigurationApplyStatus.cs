namespace MissionPlanner.Core.Setup;

/// <summary>Identifies the outcome of a frame-configuration write operation.</summary>
public enum FrameConfigurationApplyStatus
{
    /// <summary>Every requested value was confirmed by readback.</summary>
    Succeeded,

    /// <summary>No requested value was changed.</summary>
    NoChanges,

    /// <summary>A failure occurred and every confirmed write was rolled back.</summary>
    RolledBack,

    /// <summary>Some values may remain changed and require manual review.</summary>
    PartialFailure,

    /// <summary>The operation was cancelled before completion.</summary>
    Cancelled,

    /// <summary>The request was invalid or could not start.</summary>
    Failed
}
