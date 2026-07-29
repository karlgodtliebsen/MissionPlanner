namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Identifies the current phase of one parameter write.</summary>
public enum ParameterApplyPhase
{
    /// <summary>The target is being validated against the current session.</summary>
    Validating,

    /// <summary>The write request is being sent.</summary>
    Writing,

    /// <summary>The session is waiting for matching vehicle readback.</summary>
    Confirming,

    /// <summary>Processing of the target has completed.</summary>
    Completed,

    /// <summary>The target was skipped.</summary>
    Skipped
}
