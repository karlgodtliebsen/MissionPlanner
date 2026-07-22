using System.Buffers.Binary;

namespace MissionPlanner.MavLink.Decoding.Utils;

internal static class MavLinkWireCodec
{
    internal delegate T ReadElement<out T>(ReadOnlySpan<byte> payload, int offset);

    internal delegate void WriteElement<in T>(Span<byte> payload, int offset, T value);

    public static byte ReadByte(ReadOnlySpan<byte> payload, int offset) => payload[offset];

    public static sbyte ReadSByte(ReadOnlySpan<byte> payload, int offset) => unchecked((sbyte)payload[offset]);

    public static ushort ReadUInt16(ReadOnlySpan<byte> payload, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(payload[offset..]);

    public static short ReadInt16(ReadOnlySpan<byte> payload, int offset) => BinaryPrimitives.ReadInt16LittleEndian(payload[offset..]);

    public static uint ReadUInt32(ReadOnlySpan<byte> payload, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(payload[offset..]);

    public static int ReadInt32(ReadOnlySpan<byte> payload, int offset) => BinaryPrimitives.ReadInt32LittleEndian(payload[offset..]);

    public static ulong ReadUInt64(ReadOnlySpan<byte> payload, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(payload[offset..]);

    public static long ReadInt64(ReadOnlySpan<byte> payload, int offset) => BinaryPrimitives.ReadInt64LittleEndian(payload[offset..]);

    public static float ReadSingle(ReadOnlySpan<byte> payload, int offset) => BitConverter.Int32BitsToSingle(ReadInt32(payload, offset));

    public static double ReadDouble(ReadOnlySpan<byte> payload, int offset) => BitConverter.Int64BitsToDouble(ReadInt64(payload, offset));

    public static string ReadString(ReadOnlySpan<byte> payload, int offset, int length)
    {
        var value = payload.Slice(offset, length);
        var terminator = value.IndexOf((byte)0);
        return System.Text.Encoding.ASCII.GetString(terminator < 0 ? value : value[..terminator]);
    }

    public static T[] ReadArray<T>(ReadOnlySpan<byte> payload, int offset, int length, int elementSize, ReadElement<T> read)
    {
        var values = new T[length];
        for (var index = 0; index < length; index++)
        {
            values[index] = read(payload, offset + (index * elementSize));
        }

        return values;
    }

    public static void WriteByte(Span<byte> payload, int offset, byte value) => payload[offset] = value;

    public static void WriteSByte(Span<byte> payload, int offset, sbyte value) => payload[offset] = unchecked((byte)value);

    public static void WriteUInt16(Span<byte> payload, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(payload[offset..], value);

    public static void WriteInt16(Span<byte> payload, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(payload[offset..], value);

    public static void WriteUInt32(Span<byte> payload, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(payload[offset..], value);

    public static void WriteInt32(Span<byte> payload, int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(payload[offset..], value);

    public static void WriteUInt64(Span<byte> payload, int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(payload[offset..], value);

    public static void WriteInt64(Span<byte> payload, int offset, long value) => BinaryPrimitives.WriteInt64LittleEndian(payload[offset..], value);

    public static void WriteSingle(Span<byte> payload, int offset, float value) => WriteInt32(payload, offset, BitConverter.SingleToInt32Bits(value));

    public static void WriteDouble(Span<byte> payload, int offset, double value) => WriteInt64(payload, offset, BitConverter.DoubleToInt64Bits(value));

    public static void WriteString(Span<byte> payload, int offset, int length, string? value)
    {
        var destination = payload.Slice(offset, length);
        destination.Clear();
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        System.Text.Encoding.ASCII.GetBytes(value.AsSpan(0, Math.Min(value.Length, length)), destination);
    }

    public static void WriteArray<T>(Span<byte> payload, int offset, int length, int elementSize, IReadOnlyList<T>? values, WriteElement<T> write)
    {
        if (values is null)
        {
            return;
        }

        var count = Math.Min(length, values.Count);
        for (var index = 0; index < count; index++)
        {
            write(payload, offset + (index * elementSize), values[index]);
        }
    }
}
