namespace MissionPlanner.MavLink;

/// <summary>
/// Provides methods for calculating MAVLink CRC checksums.
/// </summary>
public static class MavLinkCrc
{
    /// <summary>
    /// Calculates the MAVLink CRC checksum for the given buffer and CRC extra byte.
    /// </summary>
    /// <param name="buffer">The buffer containing the data to calculate the CRC for.</param>
    /// <param name="crcExtra">The CRC extra byte specific to the MAVLink message.</param>
    /// <returns>The calculated CRC checksum.</returns>
    public static ushort Calculate(ReadOnlySpan<byte> buffer, byte crcExtra)
    {
        ushort crc = 0xFFFF;

        foreach (var b in buffer) Accumulate(b, ref crc);

        Accumulate(crcExtra, ref crc);

        return crc;
    }

    private static void Accumulate(byte value, ref ushort crc)
    {
        var tmp = (byte)(value ^ (byte)(crc & 0xFF));
        tmp ^= (byte)(tmp << 4);

        crc = (ushort)(
            (crc >> 8)
            ^ (tmp << 8)
            ^ (tmp << 3)
            ^ (tmp >> 4));
    }
}