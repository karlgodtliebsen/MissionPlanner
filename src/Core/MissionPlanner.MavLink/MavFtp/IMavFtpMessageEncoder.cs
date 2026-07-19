namespace MissionPlanner.MavLink.MavFtp;

public interface IMavFtpMessageEncoder
{
    byte[] Encode(byte targetSystem, byte targetComponent, ReadOnlySpan<byte> ftpPayload);
}
