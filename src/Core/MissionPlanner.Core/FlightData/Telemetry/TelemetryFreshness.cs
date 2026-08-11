namespace MissionPlanner.Core.FlightData.Telemetry;

/// <summary>Describes whether projected telemetry is current.</summary>
public enum TelemetryFreshness
{
    /// <summary>The value was observed within the freshness interval.</summary>
    Fresh,

    /// <summary>The last observation is older than the freshness interval.</summary>
    Stale,

    /// <summary>No value is currently available.</summary>
    Unavailable
}
