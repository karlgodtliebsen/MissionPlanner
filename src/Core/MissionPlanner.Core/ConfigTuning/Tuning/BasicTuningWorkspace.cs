namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Represents an opened Basic Tuning workspace over the shared parameter session.</summary>
/// <param name="Profile">The selected firmware-family profile.</param>
/// <param name="Session">The shared vehicle-scoped parameter session.</param>
/// <param name="Groups">The supported curated groups.</param>
public sealed record BasicTuningWorkspace(
    BasicTuningProfile Profile,
    IParameterEditSession Session,
    IReadOnlyList<ResolvedBasicTuningGroup> Groups)
{
    /// <summary>Gets every parameter presented by this workspace.</summary>
    public IReadOnlyList<string> PresentedParameterNames => Groups
        .SelectMany(group => group.Fields)
        .Select(item => item.ParameterName)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
}
