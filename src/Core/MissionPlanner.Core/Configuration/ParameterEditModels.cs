using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Configuration;

/// <summary>Represents a selectable enumerated parameter value.</summary>
/// <param name="Value">The stored numeric value.</param>
/// <param name="Label">The human-readable label.</param>
public sealed record ParameterValueOption(double Value, string Label);

/// <summary>Represents one bit of a bitmask parameter.</summary>
/// <param name="Bit">The zero-based bit index.</param>
/// <param name="Label">The human-readable label.</param>
public sealed record ParameterBitOption(int Bit, string Label);

/// <summary>Projects the firmware metadata relevant to editing one parameter.</summary>
/// <param name="DisplayName">The user-facing name.</param>
/// <param name="Description">The parameter description.</param>
/// <param name="Units">The unit symbol.</param>
/// <param name="Minimum">The inclusive minimum, when defined.</param>
/// <param name="Maximum">The inclusive maximum, when defined.</param>
/// <param name="Increment">The step increment, when defined.</param>
/// <param name="ReadOnly">Whether the parameter is read-only.</param>
/// <param name="RebootRequired">Whether changing the parameter requires a reboot.</param>
/// <param name="Options">The enumerated value options.</param>
/// <param name="Bitmask">The bitmask bit options.</param>
public sealed record ParameterFieldMetadata(
    string? DisplayName,
    string? Description,
    string? Units,
    double? Minimum,
    double? Maximum,
    double? Increment,
    bool ReadOnly,
    bool RebootRequired,
    IReadOnlyList<ParameterValueOption> Options,
    IReadOnlyList<ParameterBitOption> Bitmask)
{
    /// <summary>Gets an empty metadata projection.</summary>
    public static ParameterFieldMetadata Empty { get; } = new(null, null, null, null, null, null, false, false, [], []);
}

/// <summary>Projects the editable state of one parameter within a session.</summary>
/// <param name="Name">The parameter name.</param>
/// <param name="Type">The parameter wire type.</param>
/// <param name="LiveValue">The last confirmed on-vehicle value.</param>
/// <param name="PendingValue">The pending, not-yet-written value.</param>
/// <param name="Metadata">The firmware metadata.</param>
/// <param name="ValidationError">The current validation error, when invalid.</param>
public sealed record ParameterEditField(
    string Name,
    MavParamType Type,
    double LiveValue,
    double PendingValue,
    ParameterFieldMetadata Metadata,
    string? ValidationError)
{
    /// <summary>Gets whether the pending value differs from the live value.</summary>
    public bool IsModified => Math.Abs(PendingValue - LiveValue) > 0.0001;

    /// <summary>Gets whether the pending value is valid.</summary>
    public bool IsValid => ValidationError is null;
}

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

/// <summary>Projects the outcome of writing one parameter.</summary>
/// <param name="Name">The parameter name.</param>
/// <param name="Outcome">The write outcome.</param>
/// <param name="Message">A user-facing explanation.</param>
public sealed record ParameterWriteResult(string Name, ParameterWriteOutcome Outcome, string Message);

/// <summary>Projects the aggregate result of applying a group of parameter edits.</summary>
/// <param name="Success">Whether every modified field was confirmed.</param>
/// <param name="Results">The per-field results.</param>
/// <param name="RebootRequired">Whether any confirmed change requires a reboot.</param>
public sealed record ParameterApplyReport(bool Success, IReadOnlyList<ParameterWriteResult> Results, bool RebootRequired)
{
    /// <summary>Gets the fields that were confirmed.</summary>
    public IReadOnlyList<string> Confirmed => Results.Where(result => result.Outcome == ParameterWriteOutcome.Confirmed).Select(result => result.Name).ToArray();

    /// <summary>Gets the fields that still need attention.</summary>
    public IReadOnlyList<string> Failed => Results
        .Where(result => result.Outcome is ParameterWriteOutcome.WriteFailed or ParameterWriteOutcome.ReadbackFailed or ParameterWriteOutcome.ValidationFailed)
        .Select(result => result.Name)
        .ToArray();
}
