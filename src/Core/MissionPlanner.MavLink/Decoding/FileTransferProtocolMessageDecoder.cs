using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Provides the public API for FileTransferProtocolMessageDecoder.
/// </summary>
public sealed class FileTransferProtocolMessageDecoder : IMavLinkMessageDecoder
{
    private const int HeaderLength = 3;
    private const int FtpPayloadLength = 251;

    /// <summary>
    /// Provides the public API for MessageId.
    /// </summary>
    public uint MessageId => MessageIds.FileTransferProtocol;
    /// <summary>
    /// Provides the public API for CrcExtra.
    /// </summary>
    public byte CrcExtra => 84;

    /// <summary>
    /// Provides the public API for TryDecode.
    /// </summary>
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        if (frame.Payload.Length < HeaderLength)
        {
            message = null;
            return false;
        }

        // FILE_TRANSFER_PROTOCOL contains a fixed uint8_t payload[251]. MAVLink 2
        // may truncate trailing zero bytes from any message payload on the wire.
        // Reconstruct the fixed-length field before handing it to the MAVFTP codec;
        // otherwise valid ACK/NAK packets whose FTP header ends in zeros appear
        // shorter than the 12-byte MAVFTP header and are rejected as truncated.
        var framePayload = frame.Payload.Span;
        var ftpPayload = new byte[FtpPayloadLength];
        var encodedFtpLength = Math.Min(framePayload.Length - HeaderLength, FtpPayloadLength);
        framePayload.Slice(HeaderLength, encodedFtpLength).CopyTo(ftpPayload);

        message = new FileTransferProtocolMessage(
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            framePayload[0],
            framePayload[1],
            framePayload[2],
            ftpPayload,
            frame.ReceivedAt);
        return true;
    }
}
