namespace MissionPlanner.Core.FlightData.Scripting;

/// <summary>Describes script execution progress.</summary>
public enum VehicleScriptExecutionState
{
    /// <summary>The document is being validated.</summary>
    Validating,

    /// <summary>The document is being evaluated without side effects.</summary>
    DryRun,

    /// <summary>The document is executing.</summary>
    Running,

    /// <summary>Every executed step succeeded.</summary>
    Succeeded,

    /// <summary>Execution stopped after a failure.</summary>
    Failed,

    /// <summary>Execution was cancelled.</summary>
    Cancelled
}
