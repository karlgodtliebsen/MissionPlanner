namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Describes freshness and availability of receiver channel input.</summary>
public enum RadioSignalState
{
    /// <summary>Fresh RC channel input is available.</summary>
    Live,

    /// <summary>The last RC channel input is retained but no longer fresh.</summary>
    Stale,

    /// <summary>No RC channel input has been observed.</summary>
    NoSignal
}
