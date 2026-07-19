namespace MissionPlanner.MavLink.MavFtp.Abstractions;

/// <summary>
/// Provides methods for registering and dispatching MAVFTP responses.
/// </summary>
public interface IMavFtpResponseDispatcher : IDisposable
{
    /// <summary>
    /// Registers a MAVFTP response for a specific request.
    /// </summary>
    /// <param name="target">The target vehicle for the MAVFTP request.</param>
    /// <param name="requestSequence">The sequence number of the request.</param>
    /// <param name="requestedOpcode">The opcode of the requested MAVFTP operation.</param>
    /// <param name="session">The session identifier, if applicable.</param>
    /// <param name="multipleResponses">Indicates whether multiple responses are expected for the request.</param>
    /// <returns>A registration object for the MAVFTP response.</returns>
    MavFtpResponseRegistration Register(MavFtpTarget target, ushort requestSequence, MavFtpOpcode requestedOpcode, byte? session = null, bool multipleResponses = false);
}
