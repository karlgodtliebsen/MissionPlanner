namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Identifies the outcome of writing one parameter.</summary>
public enum ParameterWriteOutcome
{
    /// <summary>The field was not modified and was skipped.</summary>
    Unchanged,

    /// <summary>The write was confirmed by readback.</summary>
    Confirmed,

    /// <summary>The pending value failed validation and was not written.</summary>
    ValidationFailed,

    /// <summary>The write request was rejected by the vehicle.</summary>
    WriteFailed,

    /// <summary>The write was sent but not confirmed by readback.</summary>
    ReadbackFailed,

    /// <summary>The write was skipped because an earlier write failed or the session was invalid.</summary>
    Skipped
}
