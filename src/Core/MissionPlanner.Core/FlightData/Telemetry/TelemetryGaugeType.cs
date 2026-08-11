namespace MissionPlanner.Core.FlightData.Telemetry;

/// <summary>Recommends a presentation for one telemetry field.</summary>
public enum TelemetryGaugeType
{
    /// <summary>Displays the value as formatted text.</summary>
    Numeric,

    /// <summary>Displays the value on a circular dial.</summary>
    Dial,

    /// <summary>Displays the value on a linear bar.</summary>
    Bar
}
