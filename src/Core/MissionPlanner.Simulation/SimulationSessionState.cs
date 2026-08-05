namespace MissionPlanner.Core.Simulation;

/// <summary>Identifies a simulation session lifecycle state.</summary>
public enum SimulationSessionState
{
    /// <summary>No simulator session is active.</summary>
    Stopped,

    /// <summary>The selected profile and host resources are being validated.</summary>
    Validating,

    /// <summary>The runtime is creating the owned simulator session.</summary>
    Starting,

    /// <summary>The runtime is active and MissionPlanner is waiting for its heartbeat.</summary>
    WaitingForHeartbeat,

    /// <summary>The simulator is running and its expected heartbeat was observed.</summary>
    Running,

    /// <summary>The owned simulator session is stopping.</summary>
    Stopping,

    /// <summary>The simulator exited successfully without an explicit stop request.</summary>
    Completed,

    /// <summary>Validation, startup, heartbeat, runtime, or cleanup failed.</summary>
    Failed
}
