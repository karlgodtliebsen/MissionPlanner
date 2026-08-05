namespace MissionPlanner.Simulation;

/// <summary>Identifies telemetry exposed to declarative conditions.</summary>
public enum SimulationTelemetryMetric
{
    /// <summary>Whether the vehicle connection is online.</summary>
    Online,

    /// <summary>Whether the vehicle is armed.</summary>
    Armed,

    /// <summary>The semantic flight mode name.</summary>
    Mode,

    /// <summary>The extended landed-state name.</summary>
    LandedState,

    /// <summary>The primary GPS fix-type name.</summary>
    GpsFixType,

    /// <summary>Relative altitude in metres.</summary>
    RelativeAltitudeMeters,

    /// <summary>Mean-sea-level altitude in metres.</summary>
    AltitudeMslMeters,

    /// <summary>Ground speed in metres per second.</summary>
    GroundSpeedMetersPerSecond,

    /// <summary>Remaining battery percentage.</summary>
    BatteryRemainingPercent,

    /// <summary>Latitude in decimal degrees.</summary>
    LatitudeDegrees,

    /// <summary>Longitude in decimal degrees.</summary>
    LongitudeDegrees
}
