using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Validates the bridge's byte-wise string, address and integer encoding.</summary>
public static class EspBridgeSettings
{
    /// <summary>Gets editable integer parameters and their supported ranges.</summary>
    public static IReadOnlyDictionary<string, (uint Minimum, uint Maximum)> IntegerRanges { get; } =
        new Dictionary<string, (uint, uint)>
        {
            ["WIFI_MODE"] = (0, 1), ["WIFI_CHANNEL"] = (1, 13),
            ["WIFI_UDP_HPORT"] = (1, 65535), ["WIFI_UDP_CPORT"] = (1, 65535),
            ["UART_BAUDRATE"] = (1200, 3000000), ["DEBUG_ENABLED"] = (0, 1),
        };

    /// <summary>Packs a NUL-terminated UTF-8 string without truncating credentials.</summary>
    public static float[] EncodeText(string value, bool password)
    {
        var length = Encoding.UTF8.GetByteCount(value);
        if (value.Contains('\0') || length > 15 || (password && length is > 0 and < 8))
        {
            throw new ArgumentException(password
                ? "Bridge passwords must be empty for an open network, or 8–15 UTF-8 bytes."
                : "Bridge network names must fit in 15 UTF-8 bytes without embedded NUL characters.");
        }
        return PackedParameterStringCodec.Encode(value);
    }

    /// <summary>Decodes a raw integer union without a floating-point conversion.</summary>
    public static uint DecodeInteger(float raw) => unchecked((uint)BitConverter.SingleToInt32Bits(raw));

    /// <summary>Encodes a validated integer in its raw parameter union.</summary>
    public static float EncodeInteger(string name, uint value)
    {
        if (!IntegerRanges.TryGetValue(name, out var range) || value < range.Minimum || value > range.Maximum)
            throw new ArgumentOutOfRangeException(nameof(value), "The bridge setting is outside its supported range.");
        return BitConverter.Int32BitsToSingle(unchecked((int)value));
    }

    /// <summary>Encodes a dotted IPv4 address in ESP8266 IPAddress byte order.</summary>
    public static float EncodeAddress(string address)
    {
        if (!IPAddress.TryParse(address, out var parsed) || parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new ArgumentException("Enter an IPv4 address, or 0.0.0.0 for automatic addressing.");
        return BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(parsed.GetAddressBytes()));
    }

    /// <summary>Decodes the bridge's IPv4 address.</summary>
    public static string DecodeAddress(float raw)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, BitConverter.SingleToInt32Bits(raw));
        return new IPAddress(bytes).ToString();
    }
}
