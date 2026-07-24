namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Represents an opened advanced tuning workspace over the shared parameter session.</summary>
/// <param name="Profile">The selected family profile.</param>
/// <param name="Session">The shared parameter session.</param>
/// <param name="Groups">The presence-gated lazy groups.</param>
public sealed record ExtendedTuningWorkspace(
    ExtendedTuningProfile Profile,
    IParameterEditSession Session,
    IReadOnlyList<ResolvedAdvancedTuningGroup> Groups);
