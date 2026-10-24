using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace MissionPlanner.Core.Setup.OptionalHardware.DroneCan;

/// <summary>Serializes the published UAVCAN v0 management DSDL types without changing numeric parameter representations.</summary>
public static class DroneCanManagementCodec
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    /// <summary>Builds a GetSet request; an empty value reads rather than writes.</summary>
    public static byte[] GetSet(ushort index, string name = "", object? value = null)
    {
        if (index > 8191) throw new ArgumentOutOfRangeException(nameof(index));
        var text = Text(name, 92); var encoded = Value(value);
        return [(byte)index, (byte)((index >> 8) << 3 | encoded[0]), .. encoded.AsSpan(1), .. text];
    }
    /// <summary>Reads the value, default and limits of a GetSet response; an empty name marks the end of enumeration.</summary>
    public static DroneCanParameter? ReadParameter(byte node, ReadOnlySpan<byte> data)
    {
        var offset = 0;
        var value = ReadValue(data, ref offset, false);
        var defaultValue = ReadValue(data, ref offset, false);
        var maximum = ReadValue(data, ref offset, true);
        var minimum = ReadValue(data, ref offset, true);
        if (data.Length - offset > 92) throw new InvalidDataException("DroneCAN parameter name exceeds its DSDL bound.");
        var name = Utf8.GetString(data[offset..]);
        if (name.Length == 0 || value is null) return null;
        if (name.Contains('\0') || value is null) throw new InvalidDataException("Invalid DroneCAN parameter response.");
        return new(node, name, value, true) { DefaultValue = defaultValue, Minimum = minimum, Maximum = maximum };
    }
    /// <summary>Parses an editor value using the node's actual DSDL type and limits.</summary>
    public static object ParseValue(DroneCanParameter parameter, string text)
    {
        object value = parameter.Value switch
        {
            long => long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture),
            float => float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
            bool => bool.Parse(text),
            string => text,
            _ => throw new NotSupportedException("Unknown DroneCAN parameter type.")
        };
        ValidateValue(parameter, value); return value;
    }
    /// <summary>Checks the exact wire type and advertised bounds before writing.</summary>
    public static void ValidateValue(DroneCanParameter parameter, object value)
    {
        if (!parameter.IsWritable || parameter.Value.GetType() != value.GetType()) throw new ArgumentException("Preserve the node's parameter type.");
        _ = Value(value);
        if (value is long integer && (parameter.Minimum is long min && integer < min || parameter.Maximum is long max && integer > max) ||
            value is float real && (parameter.Minimum is float low && real < low || parameter.Maximum is float high && real > high))
            throw new ArgumentOutOfRangeException(nameof(value), "Value is outside the node's reported limits.");
    }
    /// <summary>Formats a value for invariant, round-trip editing.</summary>
    public static string Format(object value) => value is float number ? number.ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    /// <summary>Reads the fixed fields and bounded certificate/name of GetNodeInfo.</summary>
    public static (string Name, string Version, string UniqueId) NodeInfo(ReadOnlySpan<byte> data)
    {
        if (data.Length < 41) throw new InvalidDataException("Truncated DroneCAN node information.");
        var nameOffset = 41 + data[40];
        if (nameOffset > data.Length || data.Length - nameOffset is < 1 or > 80) throw new InvalidDataException("Invalid DroneCAN node name/certificate.");
        var name = Utf8.GetString(data[nameOffset..]);
        if (name.Any(char.IsControl)) throw new InvalidDataException("Invalid DroneCAN node name.");
        return (name, $"{data[7]}.{data[8]}", Convert.ToHexString(data.Slice(24, 16)));
    }
    /// <summary>Checks a bounded DSDL text field, including its UTF-8 byte length.</summary>
    public static byte[] Text(string value, int maximum)
    {
        var bytes = Utf8.GetBytes(value);
        if (bytes.Length > maximum || value.Contains('\0')) throw new ArgumentException($"Text must contain at most {maximum} UTF-8 bytes and no NUL.");
        return bytes;
    }
    private static byte[] Value(object? value)
    {
        switch (value)
        {
            case null: return [0];
            case long number:
                var integer = new byte[9]; integer[0] = 1; BinaryPrimitives.WriteInt64LittleEndian(integer.AsSpan(1), number); return integer;
            case float number when float.IsFinite(number):
                var real = new byte[5]; real[0] = 2; BinaryPrimitives.WriteSingleLittleEndian(real.AsSpan(1), number); return real;
            case bool boolean: return [3, boolean ? (byte)1 : (byte)0];
            case string text: var bytes = Text(text, 128); return [4, (byte)bytes.Length, .. bytes];
            default: throw new ArgumentException("Supported values are int64, finite float32, Boolean and UTF-8 string.");
        }
    }
    private static object? ReadValue(ReadOnlySpan<byte> data, ref int offset, bool numeric)
    {
        if (offset >= data.Length) throw new InvalidDataException("Truncated DroneCAN parameter.");
        var tag = data[offset++]; // DSDL void5/void6 padding precedes the union tag.
        if (tag > (numeric ? 2 : 4)) throw new InvalidDataException("Invalid DroneCAN union or padding.");
        var length = tag switch { 0 => 0, 1 => 8, 2 => 4, 3 => 1, 4 when offset < data.Length => 1 + data[offset], _ => -1 };
        if (length < 0 || offset + length > data.Length || tag == 4 && length > 129) throw new InvalidDataException("Truncated or oversized DroneCAN value.");
        object? value = tag switch
        {
            0 => null, 1 => BinaryPrimitives.ReadInt64LittleEndian(data[offset..]), 2 => BinaryPrimitives.ReadSingleLittleEndian(data[offset..]),
            3 when data[offset] <= 1 => data[offset] != 0, 4 => Utf8.GetString(data.Slice(offset + 1, length - 1)),
            _ => throw new InvalidDataException("Invalid DroneCAN Boolean.")
        };
        if (value is float number && !float.IsFinite(number)) throw new InvalidDataException("Non-finite DroneCAN parameter.");
        offset += length; return value;
    }
}
