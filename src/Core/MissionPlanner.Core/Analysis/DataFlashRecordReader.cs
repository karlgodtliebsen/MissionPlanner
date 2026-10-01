using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MissionPlanner.Core.Analysis;

/// <summary>Reads self-describing DataFlash binary or FMT-based text exports for offline analysis.</summary>
public sealed class DataFlashRecordReader
{
    private sealed record Format(int Length, string Name, string Types, string[] Columns);

    internal IEnumerable<DataFlashRecord> Read(Stream stream, bool binary, CancellationToken token)
    {
        return binary ? ReadBinary(stream, token) : ReadText(stream, token);
    }

    private static bool Relevant(string name)
    {
        return name is "ISBH" or "ISBD" or "RPM" or "RCOU" or "PARM" ||
        name.StartsWith("IMU", StringComparison.Ordinal) || name.StartsWith("ACC", StringComparison.Ordinal) ||
        name.StartsWith("GYR", StringComparison.Ordinal) || name.StartsWith("ESC", StringComparison.Ordinal);
    }

    private static IEnumerable<DataFlashRecord> ReadBinary(Stream stream, CancellationToken token)
    {
        var formats = new Dictionary<int, Format> { [128] = new(89, "FMT", "BBnNZ", ["Type", "Length", "Name", "Format", "Columns"]) };
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        long records = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var first = stream.ReadByte();
            if (first < 0)
            {
                yield break;
            }
            if (++records > 10_000_000)
            {
                throw new InvalidDataException("Log exceeds the analysis record limit.");
            }
            if (first != 0xA3 || stream.ReadByte() != 0x95)
            {
                throw new InvalidDataException("Invalid DataFlash packet boundary; recover the damaged log before analysis.");
            }
            var id = stream.ReadByte();
            if (!formats.ContainsKey(id) && stream.CanSeek && FindLaterFormat(stream, id, token) is { } laterFormat)
            {
                formats[id] = laterFormat;
            }
            if (!formats.TryGetValue(id, out var format) || format.Length < 3)
            {
                throw new InvalidDataException($"Missing FMT definition for message {id}.");
            }
            var payload = reader.ReadBytes(format.Length - 3);
            if (payload.Length != format.Length - 3)
            {
                yield return new DataFlashRecord("TRUNCATED", new Dictionary<string, object>());
                yield break;
            }
            if (id == 128)
            {
                if (payload.Length != 86)
                {
                    throw new InvalidDataException("Invalid FMT packet.");
                }
                var name = Text(payload.AsSpan(2, 4));
                var types = Text(payload.AsSpan(6, 16));
                var columns = Text(payload.AsSpan(22, 64)).Split(',');
                if (types.Length != columns.Length || payload[1] < 3)
                {
                    throw new InvalidDataException("Invalid FMT column definition.");
                }
                formats[payload[0]] = new Format(payload[1], name, types, columns);
            }
            else if (Relevant(format.Name))
            {
                var fields = new Dictionary<string, object>(StringComparer.Ordinal);
                var offset = 0;
                for (var i = 0; i < format.Types.Length; i++)
                {
                    fields[format.Columns[i]] = Decode(payload, ref offset, format.Types[i]);
                }
                yield return offset != payload.Length
                    ? throw new InvalidDataException($"FMT length mismatch for {format.Name}.")
                    : new DataFlashRecord(format.Name, fields);
            }
        }
    }

    // Some firmware emits a message before logging its FMT. Look ahead without
    // consuming records, then resume decoding at the original payload position.
    private static Format? FindLaterFormat(Stream stream, int id, CancellationToken token)
    {
        var position = stream.Position;
        var buffer = new byte[65536 + 88];
        var retained = 0;
        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var read = stream.Read(buffer, retained, buffer.Length - retained);
                var available = retained + read;
                for (var i = 0; i + 89 <= available; i++)
                {
                    if (buffer[i] != 0xA3 || buffer[i + 1] != 0x95 || buffer[i + 2] != 128 || buffer[i + 3] != id)
                    {
                        continue;
                    }
                    var payload = buffer.AsSpan(i + 3, 86);
                    var name = Text(payload.Slice(2, 4));
                    var types = Text(payload.Slice(6, 16));
                    var columns = Text(payload.Slice(22, 64)).Split(',');
                    if (payload[1] >= 3 && name.Length > 0 && types.Length > 0 && types.Length == columns.Length &&
                        types.All(c => "bBMhHcCiIeELfnqQdNZa".Contains(c)))
                    {
                        return new Format(payload[1], name, types, columns);
                    }
                }
                if (read == 0)
                {
                    return null;
                }
                retained = Math.Min(88, available);
                buffer.AsSpan(available - retained, retained).CopyTo(buffer);
            }
        }
        finally
        {
            stream.Position = position;
        }
    }

    private static string Text(ReadOnlySpan<byte> bytes)
    {
        return Encoding.ASCII.GetString(bytes).TrimEnd('\0').Trim();
    }

    private static object Decode(byte[] payload, ref int offset, char type)
    {
        var size = type switch
        {
            'b' or 'B' or 'M' => 1,
            'h' or 'H' or 'c' or 'C' => 2,
            'i' or 'I' or 'e' or 'E' or 'L' or 'f' or 'n' => 4,
            'q' or 'Q' or 'd' => 8,
            'N' => 16,
            'Z' or 'a' => 64,
            _ => throw new InvalidDataException($"Unsupported DataFlash field format '{type}'.")
        };
        if (offset + size > payload.Length)
        {
            throw new InvalidDataException("FMT field exceeds packet length.");
        }
        var bytes = payload.AsSpan(offset, size);
        offset += size;
        if (type == 'a')
        {
            var values = new double[32];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes[(i * 2)..]);
            }
            return values;
        }
        return type switch
        {
            'n' or 'N' or 'Z' => Text(bytes),
            'b' => (double)(sbyte)bytes[0],
            'B' or 'M' => (double)bytes[0],
            'h' => (double)BinaryPrimitives.ReadInt16LittleEndian(bytes),
            'H' => (double)BinaryPrimitives.ReadUInt16LittleEndian(bytes),
            'c' => BinaryPrimitives.ReadInt16LittleEndian(bytes) * 0.01,
            'C' => BinaryPrimitives.ReadUInt16LittleEndian(bytes) * 0.01,
            'i' => (double)BinaryPrimitives.ReadInt32LittleEndian(bytes),
            'I' => (double)BinaryPrimitives.ReadUInt32LittleEndian(bytes),
            'e' => BinaryPrimitives.ReadInt32LittleEndian(bytes) * 0.01,
            'E' => BinaryPrimitives.ReadUInt32LittleEndian(bytes) * 0.01,
            'L' => BinaryPrimitives.ReadInt32LittleEndian(bytes) * 1e-7,
            'q' => (double)BinaryPrimitives.ReadInt64LittleEndian(bytes),
            'Q' => (double)BinaryPrimitives.ReadUInt64LittleEndian(bytes),
            'f' => (double)BinaryPrimitives.ReadSingleLittleEndian(bytes),
            'd' => BinaryPrimitives.ReadDoubleLittleEndian(bytes),
            _ => throw new InvalidDataException("Unsupported field.")
        };
    }

    private static IEnumerable<DataFlashRecord> ReadText(Stream stream, CancellationToken token)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, leaveOpen: true);
        var formats = new Dictionary<string, Format>(StringComparer.Ordinal);
        long lines = 0;
        while (reader.ReadLine() is { } line)
        {
            token.ThrowIfCancellationRequested();
            if (++lines > 10_000_000 || line.Length > 65536)
            {
                throw new InvalidDataException("DataFlash text exceeds the analysis input limit.");
            }
            var items = Regex.Split(line, @",(?![^\[]*\])").Select(s => s.Trim()).ToArray();
            if (items.Length == 0 || line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            if (items[0] == "FMT")
            {
                if (items.Length < 6 || items[4].Length != items.Length - 5)
                {
                    throw new InvalidDataException($"Invalid FMT on line {lines}.");
                }
                formats[items[3]] = new Format(0, items[3], items[4], items[5..]);
            }
            else if (Relevant(items[0]))
            {
                if (!formats.TryGetValue(items[0], out var format) || items.Length != format.Columns.Length + 1)
                {
                    throw new InvalidDataException($"Missing FMT or invalid field count on line {lines}.");
                }
                var fields = new Dictionary<string, object>(StringComparer.Ordinal);
                for (var i = 0; i < format.Columns.Length; i++)
                {
                    var value = items[i + 1];
                    fields[format.Columns[i]] = format.Types[i] switch
                    {
                        'n' or 'N' or 'Z' => value,
                        'a' => value.Trim('[', ']').Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
                            .Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray(),
                        _ => double.Parse(value, CultureInfo.InvariantCulture)
                    };
                }
                yield return new DataFlashRecord(format.Name, fields);
            }
        }
    }
}
