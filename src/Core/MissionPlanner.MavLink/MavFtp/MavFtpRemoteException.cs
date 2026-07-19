namespace MissionPlanner.MavLink.MavFtp;

public sealed class MavFtpRemoteException : Exception
{
    public MavFtpRemoteException(MavFtpNakError error, MavFtpPacket packet, string? remotePath = null)
        : base($"MAVFTP {packet.RequestedOpcode} failed with {error}{(remotePath is null ? string.Empty : $" for '{remotePath}'")}.")
    {
        Error = error;
        Packet = packet;
        RemotePath = remotePath;
    }

    public MavFtpNakError Error { get; }
    public MavFtpPacket Packet { get; }
    public string? RemotePath { get; }
}
