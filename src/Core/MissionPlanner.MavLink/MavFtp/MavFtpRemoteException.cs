namespace MissionPlanner.MavLink.MavFtp;

/// <summary>
/// Represents an exception that occurs when a MAVFTP operation fails on the remote vehicle.
/// </summary>
public sealed class MavFtpRemoteException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MavFtpRemoteException"/> class.
    /// </summary>
    /// <param name="error">The MAVFTP error code.</param>
    /// <param name="packet">The MAVFTP packet associated with the error.</param>
    /// <param name="remotePath">The remote path involved in the error, if applicable.</param>
    public MavFtpRemoteException(MavFtpNakError error, MavFtpPacket packet, string? remotePath = null)
        : base($"MAVFTP {packet.RequestedOpcode} failed with {error}{(remotePath is null ? string.Empty : $" for '{remotePath}'")}.")
    {
        Error = error;
        Packet = packet;
        RemotePath = remotePath;
    }

    /// <summary>
    /// Gets the MAVFTP error code associated with the exception.
    /// </summary>
    public MavFtpNakError Error { get; }

    /// <summary>
    /// Gets the MAVFTP packet associated with the exception.
    /// </summary>
    public MavFtpPacket Packet { get; }

    /// <summary>
    /// Gets the remote path involved in the error, if applicable.
    /// </summary>
    public string? RemotePath { get; }
}
