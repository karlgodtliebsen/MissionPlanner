namespace MissionPlanner.App.Maps;

/// <summary>Identifies a normal Mapsui source-creation outcome.</summary>
public enum MapBasemapCreationStatus
{
    /// <summary>The layer was created.</summary>
    Success,

    /// <summary>The current raster renderer does not support the source.</summary>
    Unsupported,

    /// <summary>Reviewed policy denied use.</summary>
    PolicyDenied,

    /// <summary>A required credential is missing.</summary>
    CredentialMissing,

    /// <summary>The endpoint or archive is unavailable.</summary>
    SourceUnavailable,

    /// <summary>The source definition is invalid.</summary>
    InvalidConfiguration,

    /// <summary>The renderer could not construct the source.</summary>
    RendererFailure,

    /// <summary>Creation was cancelled.</summary>
    Cancelled
}
