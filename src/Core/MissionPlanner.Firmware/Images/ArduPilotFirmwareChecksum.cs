namespace MissionPlanner.Firmware.Images;

/// <summary>Calculates the checksum expected by the ArduPilot PX4 bootloader protocol.</summary>
/// <remarks>
/// Ported from ArduPilot <c>Tools/scripts/uploader.py</c>, copyright 2012-2017 PX4 Development
/// Team and distributed under the BSD 3-Clause licence, and cross-checked against Mission
/// Planner <c>px4uploader/Firmware.cs</c>, distributed under GPL-3.0. The implementation retains
/// the algorithm's attribution; repository distribution remains subject to its licence notices.
/// </remarks>
public static class ArduPilotFirmwareChecksum
{
    private const uint Polynomial = 0xedb88320;

    /// <summary>Calculates the protocol CRC, padding application flash with erased bytes.</summary>
    /// <param name="image">Unpadded application bytes.</param>
    /// <param name="flashLength">Bootloader-reported application flash length.</param>
    /// <returns>The protocol-compatible checksum state.</returns>
    public static uint Calculate(ReadOnlySpan<byte> image, int flashLength)
    {
        if (flashLength < image.Length) throw new ArgumentOutOfRangeException(nameof(flashLength));
        var state = Update(0, image);
        Span<byte> padding = stackalloc byte[4] { 0xff, 0xff, 0xff, 0xff };
        var alignmentBytes = Align4(image.Length) - image.Length;
        if (alignmentBytes > 0) state = Update(state, padding[..alignmentBytes]);
        for (var offset = Align4(image.Length); offset < flashLength - 1; offset += 4) state = Update(state, padding);
        return state;
    }

    /// <summary>Updates an existing uploader-compatible checksum state.</summary>
    public static uint Update(uint state, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            state = Table[(state ^ value) & 0xff] ^ (state >> 8);
        }
        return state;
    }

    private static int Align4(int length) => checked((length + 3) & ~3);
    private static readonly uint[] Table = CreateTable();
    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++) value = (value & 1) != 0 ? Polynomial ^ (value >> 1) : value >> 1;
            table[index] = value;
        }
        return table;
    }
}
