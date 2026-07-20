namespace MissionPlanner.MavLink.MavFtp.Abstractions;

/// <summary>
/// Retains MAVFTP protocol sequence state independently of connection-scoped clients.
/// </summary>
public interface IMavFtpSequenceStore
{
    ushort GetNextRequest(MavFtpTarget target);
    void ObserveResponse(MavFtpTarget target, ushort responseSequence);
}
