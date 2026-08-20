namespace MissionPlanner.Core.Setup.Definitions;

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

    /// <summary>Servo and actuator output configuration.</summary>
    ServoOutput,

    /// <summary>
    /// 
    /// </summary>
    SerialPorts,

    /// <summary>Electronic speed controller configuration.</summary>
    Esc,

    /// <summary>Vehicle failsafe configuration.</summary>
    FailSafe,
    InitialTuneParameters,
    HWId,

    ADSB,

    /// <summary>Flight-mode configuration.</summary>
    FlightModes,

    /// <summary>Battery monitor configuration.</summary>
    Battery,

    /// <summary>Optional peripheral hardware.</summary>
    OptionalHardware,

    /// <summary>Safety checks and settings.</summary>
    Safety,

    /// <summary>Setup completion summary.</summary>
    Summary
}
