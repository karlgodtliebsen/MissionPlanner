using System.Buffers.Binary;

namespace MissionPlanner.MavLink.Decoding;

internal static class MavLinkDecoderHelpers
{
    public static float ReadSingle(ReadOnlySpan<byte> span)
    {
        var raw = BinaryPrimitives.ReadInt32LittleEndian(span);
        return BitConverter.Int32BitsToSingle(raw);
    }

    public static ushort ReadUInt16OrDefault(ReadOnlySpan<byte> span, int offset, ushort defaultValue = 0)
        => span.Length >= offset + 2 ? BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2)) : defaultValue;

    public static short ReadInt16OrDefault(ReadOnlySpan<byte> span, int offset, short defaultValue = 0)
        => span.Length >= offset + 2 ? BinaryPrimitives.ReadInt16LittleEndian(span.Slice(offset, 2)) : defaultValue;

    public static uint ReadUInt32OrDefault(ReadOnlySpan<byte> span, int offset, uint defaultValue = 0)
        => span.Length >= offset + 4 ? BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4)) : defaultValue;

    public static int ReadInt32OrDefault(ReadOnlySpan<byte> span, int offset, int defaultValue = 0)
        => span.Length >= offset + 4 ? BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset, 4)) : defaultValue;

    public static ulong ReadUInt64OrDefault(ReadOnlySpan<byte> span, int offset, ulong defaultValue = 0)
        => span.Length >= offset + 8 ? BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(offset, 8)) : defaultValue;

    public static long ReadInt64OrDefault(ReadOnlySpan<byte> span, int offset, long defaultValue = 0)
        => span.Length >= offset + 8 ? BinaryPrimitives.ReadInt64LittleEndian(span.Slice(offset, 8)) : defaultValue;

    public static float ReadSingleOrDefault(ReadOnlySpan<byte> span, int offset, float defaultValue = 0)
        => span.Length >= offset + 4 ? ReadSingle(span.Slice(offset, 4)) : defaultValue;

    public static byte ReadByteOrDefault(ReadOnlySpan<byte> span, int offset, byte defaultValue = 0)
        => span.Length > offset ? span[offset] : defaultValue;

    public static sbyte ReadSByteOrDefault(ReadOnlySpan<byte> span, int offset, sbyte defaultValue = 0)
        => span.Length > offset ? unchecked((sbyte)span[offset]) : defaultValue;
}
