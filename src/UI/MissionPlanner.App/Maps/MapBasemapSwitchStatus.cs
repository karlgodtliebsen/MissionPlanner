namespace MissionPlanner.App.Maps;

/// <summary>Identifies the outcome of an asynchronous basemap request.</summary>
public enum MapBasemapSwitchStatus
{
    /// <summary>The requested source was committed.</summary>
    Success,

    /// <summary>The source could not be resolved.</summary>
    ResolutionFailed,

    /// <summary>The renderer could not create the source.</summary>
    CreationFailed,

    /// <summary>A newer source request superseded this request.</summary>
    Superseded,

    /// <summary>The caller cancelled the request.</summary>
    Cancelled
}
