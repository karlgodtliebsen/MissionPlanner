namespace MissionPlanner.Core.Setup;

/// <summary>Identifies a workflow in the initial-setup workspace.</summary>
public enum SetupWorkflowKey
{
    /// <summary>Firmware installation and identity.</summary>
    Firmware,
    /// <summary>Vehicle frame selection.</summary>
    Frame,
    /// <summary>Accelerometer calibration.</summary>
    Accelerometer,
    /// <summary>Compass calibration.</summary>
    Compass,
    /// <summary>Radio calibration.</summary>
    Radio,
    /// <summary>Flight-mode configuration.</summary>
    FlightModes,
    /// <summary>Battery monitor configuration.</summary>
    Battery,
    /// <summary>Electronic speed controller configuration.</summary>
    Esc,
    /// <summary>Servo and actuator output configuration.</summary>
    ServoOutput,
    /// <summary>Optional peripheral hardware.</summary>
    OptionalHardware,
    /// <summary>Safety checks and settings.</summary>
    Safety,
    /// <summary>Setup completion summary.</summary>
    Summary
}
