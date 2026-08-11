namespace MissionPlanner.Maps.Sources;

/// <summary>Identifies a normal map-source resolution outcome.</summary>
public enum MapSourceResolutionStatus
{
    /// <summary>The source resolved successfully.</summary>
    None,

    /// <summary>No matching source exists.</summary>
    UnknownSource,

    /// <summary>The source is disabled.</summary>
    Disabled,

    /// <summary>The source is intentionally deferred.</summary>
    Deferred,

    /// <summary>A required credential is not configured.</summary>
    CredentialMissing,

    /// <summary>An installed pack is missing.</summary>
    PackMissing,

    /// <summary>A custom source is missing.</summary>
    CustomSourceMissing,

    /// <summary>Reviewed policy denies interactive use.</summary>
    PolicyDenied,

    /// <summary>The source definition is invalid.</summary>
    InvalidDefinition,

    /// <summary>The current renderer cannot display the source.</summary>
    UnsupportedByRenderer,

    /// <summary>The operation was cancelled.</summary>
    Cancelled
}
