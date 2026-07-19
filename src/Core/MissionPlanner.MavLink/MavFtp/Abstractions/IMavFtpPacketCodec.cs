namespace MissionPlanner.MavLink.MavFtp.Abstractions;

/// <summary>
/// Provides methods for encoding and decoding MAVFTP packets.
/// </summary>
public interface IMavFtpPacketCodec
{
    /// <summary>
    /// Gets the maximum length of the data payload in a MAVFTP packet.
    /// </summary>
    int MaximumDataLength { get; }

    /// <summary>
    /// Encodes a MAVFTP packet into a byte array.
    /// </summary>
    /// <param name="packet">The MAVFTP packet to encode.</param>
    /// <returns>A byte array representing the encoded MAVFTP packet.</returns>
    byte[] Encode(MavFtpPacket packet);

    /// <summary>
    /// Decodes a byte array into a MAVFTP packet.
    /// </summary>
    /// <param name="payload">The byte array containing the MAVFTP packet data.</param>
    /// <returns>The decoded MAVFTP packet.</returns>
    MavFtpPacket Decode(ReadOnlySpan<byte> payload);
}
