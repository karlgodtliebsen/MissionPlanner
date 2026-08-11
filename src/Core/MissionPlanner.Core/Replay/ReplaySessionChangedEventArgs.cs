namespace MissionPlanner.Core.Replay;

/// <summary>Provides replay state-change event data.</summary>
/// <param name="snapshot">The new immutable replay state.</param>
public sealed class ReplaySessionChangedEventArgs(ReplaySessionSnapshot snapshot) : EventArgs
{
    /// <summary>Gets the new replay state.</summary>
    public ReplaySessionSnapshot Snapshot { get; } = snapshot;
}
