namespace MissionPlanner.MavLink.MavFtp.Abstractions;

/// <summary>
/// Encodes MAVFTP messages.
/// </summary>
public interface IMavFtpMessageEncoder
{
    /// <summary>
    /// Encodes a MAVFTP message.
    /// </summary>
    /// <param name="targetSystem">The target system ID.</param>
    /// <param name="targetComponent">The target component ID.</param>
    /// <param name="ftpPayload">The FTP payload.</param>
    /// <returns>The encoded MAVFTP message.</returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    byte[] Encode(byte targetSystem, byte targetComponent, ReadOnlySpan<byte> ftpPayload);
}
