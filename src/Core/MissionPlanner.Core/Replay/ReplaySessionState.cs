namespace MissionPlanner.Core.Replay;

/// <summary>Identifies the lifecycle state of an isolated telemetry-log replay.</summary>
public enum ReplaySessionState
{
    /// <summary>No telemetry log is loaded and outbound transmission is allowed.</summary>
    Unloaded,

    /// <summary>The telemetry log is being structurally indexed.</summary>
    Indexing,

    /// <summary>The indexed log is ready at its current position.</summary>
    Ready,

    /// <summary>Frames are advancing according to the replay clock.</summary>
    Playing,

    /// <summary>Playback is paused at an indexed position.</summary>
    Paused,

    /// <summary>Every indexed frame has been replayed.</summary>
    Completed,

    /// <summary>Indexing or playback failed and the log remains read-only.</summary>
    Failed
}
