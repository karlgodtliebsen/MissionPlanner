using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Replay;

/// <summary>Blocks every outbound MAVLink connection while a read-only replay is loaded.</summary>
public sealed class ReplayTransmissionPolicy(IReplaySessionManager replaySessionManager) : IMavLinkTransmissionPolicy
{
    /// <inheritdoc />
    public void ThrowIfTransmissionProhibited()
    {
        if (replaySessionManager.Snapshot.IsTransmissionProhibited)
        {
            throw new MavLinkTransmissionProhibitedException(
                "Outbound MAVLink transmission is disabled while telemetry-log replay is active. Close the replay before sending to a live or simulated vehicle.");
        }
    }
}
