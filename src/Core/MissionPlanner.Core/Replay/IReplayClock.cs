namespace MissionPlanner.Core.Replay;

/// <summary>Exposes the current deterministic replay clock.</summary>
public interface IReplayClock
{
    /// <summary>Gets the replay clock, or <see langword="null"/> when no log is loaded.</summary>
    ReplayClockSnapshot? Current { get; }
}
