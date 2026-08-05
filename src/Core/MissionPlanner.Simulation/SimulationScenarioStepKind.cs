namespace MissionPlanner.Simulation;

/// <summary>Identifies a declarative simulation scenario step.</summary>
public enum SimulationScenarioStepKind
{
    /// <summary>Waits for a named connection or flight state.</summary>
    WaitForState,

    /// <summary>Changes to a named firmware-supported mode.</summary>
    SetMode,

    /// <summary>Arms the selected vehicle.</summary>
    Arm,

    /// <summary>Starts a confirmed takeoff.</summary>
    Takeoff,

    /// <summary>Uploads embedded, typed MAVLink mission items.</summary>
    UploadMission,

    /// <summary>Starts the uploaded mission through an acknowledged MAVLink command.</summary>
    StartMission,

    /// <summary>Waits for a typed telemetry condition.</summary>
    WaitCondition,

    /// <summary>Applies a documented bounded simulation control.</summary>
    InjectFault,

    /// <summary>Resets a previously injected simulation control.</summary>
    ClearFault,

    /// <summary>Commands the selected vehicle to land.</summary>
    Land,

    /// <summary>Waits for and records a required telemetry assertion.</summary>
    AssertTelemetry
}
