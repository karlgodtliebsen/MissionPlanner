namespace MissionPlanner.Core.FlightData.Telemetry;

/// <summary>Groups promoted telemetry fields for presentation.</summary>
public enum TelemetryFieldCategory
{
    /// <summary>Flight-state telemetry.</summary>
    Flight,

    /// <summary>Geographic position and altitude telemetry.</summary>
    Position,

    /// <summary>Attitude and motion telemetry.</summary>
    Motion,

    /// <summary>Route and target-navigation telemetry.</summary>
    Navigation,

    /// <summary>Global positioning system telemetry.</summary>
    Gps,

    /// <summary>Electrical power telemetry.</summary>
    Power,

    /// <summary>Radio-link telemetry.</summary>
    Radio,

    /// <summary>Vehicle-health telemetry.</summary>
    Health,

    /// <summary>Environmental telemetry.</summary>
    Environment
}
