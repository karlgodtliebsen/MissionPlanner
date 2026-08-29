namespace MissionPlanner.Core.ConfigTuning;

/// <summary>
/// Immutable, vehicle-scoped snapshot shown to the user before any write starts.
/// </summary>
public sealed record ParameterWritePlan(
    ParameterEditScope Scope,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ParameterWritePlanEntry> Entries)
{
    /// <summary>Gets modified fields excluded from writing, with the reason for each exclusion.</summary>
    public IReadOnlyList<ParameterWriteResult> Skipped { get; init; } = [];

    /// <summary>Gets the number of changes requiring a reboot.</summary>
    public int RebootRequiredCount => Entries.Count(entry => entry.RebootRequired);

    /// <summary>Gets the planned parameter names in write order.</summary>
    public IReadOnlyList<string> Names => Entries.Select(entry => entry.Name).ToArray();
}
