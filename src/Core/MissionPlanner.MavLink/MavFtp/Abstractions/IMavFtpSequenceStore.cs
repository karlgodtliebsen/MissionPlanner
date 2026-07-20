namespace MissionPlanner.MavLink.MavFtp.Abstractions;

/// <summary>
/// Retains MAVFTP protocol sequence state independently of connection-scoped clients.
/// </summary>
public interface IMavFtpSequenceStore
{
    /// <summary>
    /// Provides the public API for GetNextRequest.
    /// </summary>
    ushort GetNextRequest(MavFtpTarget target);
    /// <summary>
    /// Provides the public API for ObserveResponse.
    /// </summary>
    void ObserveResponse(MavFtpTarget target, ushort responseSequence);
}
