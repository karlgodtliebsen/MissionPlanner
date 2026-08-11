using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Replay;

/// <summary>Contains the immutable state of one isolated telemetry-log replay.</summary>
/// <param name="SessionId">Replay-session identity.</param>
/// <param name="State">Replay lifecycle state.</param>
/// <param name="Index">Loaded telemetry-log index.</param>
/// <param name="NextFrameIndex">Index of the next frame to process.</param>
/// <param name="Clock">Current replay clock.</param>
/// <param name="Vehicles">Read-only vehicle states projected only from replay frames.</param>
/// <param name="DecodedFrames">Number of successfully decoded frames since the latest load or seek.</param>
/// <param name="RejectedFrames">Number of structurally indexed frames rejected by parser or decoder.</param>
/// <param name="Message">User-facing status.</param>
/// <param name="Failure">Failure detail when applicable.</param>
public sealed record ReplaySessionSnapshot(
    Guid SessionId,
    ReplaySessionState State,
    TelemetryLogIndex? Index,
    int NextFrameIndex,
    ReplayClockSnapshot? Clock,
    IReadOnlyList<VehicleState> Vehicles,
    long DecodedFrames,
    long RejectedFrames,
    string Message,
    string? Failure)
{
    /// <summary>Gets whether a loaded replay currently prohibits every outbound MAVLink send.</summary>
    public bool IsTransmissionProhibited => State != ReplaySessionState.Unloaded;

    /// <summary>Gets fractional playback progress from zero through one.</summary>
    public double Progress => Index is not { Entries.Count: > 0 } index
        ? 0
        : Math.Clamp(NextFrameIndex / (double)index.Entries.Count, 0, 1);

    /// <summary>Gets the initial unloaded replay state.</summary>
    public static ReplaySessionSnapshot Unloaded { get; } = new(
        Guid.Empty,
        ReplaySessionState.Unloaded,
        null,
        0,
        null,
        [],
        0,
        0,
        "No telemetry log is loaded. Live and simulation transmission is enabled.",
        null);
}
