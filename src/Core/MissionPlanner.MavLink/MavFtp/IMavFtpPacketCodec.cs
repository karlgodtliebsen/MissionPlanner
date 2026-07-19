namespace MissionPlanner.MavLink.MavFtp;

public interface IMavFtpPacketCodec
{
    int MaximumDataLength { get; }
    byte[] Encode(MavFtpPacket packet);
    MavFtpPacket Decode(ReadOnlySpan<byte> payload);
}
