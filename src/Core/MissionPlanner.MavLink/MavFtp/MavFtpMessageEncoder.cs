using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.MavFtp;

public sealed class MavFtpMessageEncoder(IMavLinkCrcExtraProvider crcExtraProvider) : IMavFtpMessageEncoder
{
    private int sequence;

    public byte[] Encode(byte targetSystem, byte targetComponent, ReadOnlySpan<byte> ftpPayload)
    {
        if (ftpPayload.Length > 251)
        {
            throw new ArgumentOutOfRangeException(nameof(ftpPayload));
        }

        var payload = new byte[3 + ftpPayload.Length];
        payload[0] = 0;
        payload[1] = targetSystem;
        payload[2] = targetComponent;
        ftpPayload.CopyTo(payload.AsSpan(3));

        var packet = new byte[10 + payload.Length + 2];
        packet[0] = 0xFD;
        packet[1] = checked((byte)payload.Length);
        packet[4] = unchecked((byte)Interlocked.Increment(ref sequence));
        packet[5] = 255;
        packet[6] = 190;
        packet[7] = (byte)MessageIds.FileTransferProtocol;
        payload.CopyTo(packet.AsSpan(10));
        if (!crcExtraProvider.TryGetCrcExtra(MessageIds.FileTransferProtocol, out var crcExtra))
        {
            throw new InvalidOperationException("FILE_TRANSFER_PROTOCOL CRC extra is not registered.");
        }

        var crc = MavLinkCrc.Calculate(packet.AsSpan(1, 9 + payload.Length), crcExtra);
        packet[^2] = (byte)crc;
        packet[^1] = (byte)(crc >> 8);
        return packet;
    }
}
