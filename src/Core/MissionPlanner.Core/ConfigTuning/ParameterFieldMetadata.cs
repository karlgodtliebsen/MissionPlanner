namespace MissionPlanner.Core.ConfigTuning;

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
    /// <summary>Gets the descriptive unit text supplied by the firmware metadata.</summary>
    public string? UnitText { get; init; }

    /// <summary>Gets the original range expression supplied by the firmware metadata.</summary>
    public string? RangeText { get; init; }

    /// <summary>Gets the original enumerated-values expression supplied by the firmware metadata.</summary>
    public string? ValuesText { get; init; }

    /// <summary>Gets the original bitmask expression supplied by the firmware metadata.</summary>
    public string? BitmaskText { get; init; }

    /// <summary>Gets the original increment expression supplied by the firmware metadata.</summary>
    public string? IncrementText { get; init; }

    /// <summary>Gets the firmware metadata user level, such as Standard or Advanced.</summary>
    public string? UserLevel { get; init; }

    /// <summary>Gets an empty metadata projection.</summary>
    public static ParameterFieldMetadata Empty { get; } = new(null, null, null, null, null, null, false, false, [], []);
}
