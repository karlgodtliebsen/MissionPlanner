using System.Buffers.Binary;
using System.Text;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Version-aware bounded decoders for Betaflight identity responses.</summary>
public static class BetaflightIdentityParser
{
    /// <summary>Reads the legacy prefix and optional versioned board fields, rejecting incomplete fields.</summary>
    public static BetaflightBoardInfo? ParseBoard(ReadOnlySpan<byte> data, Version api)
    {
        if (data.Length < 6)
        {
            return null;
        }
        var identifier = Text(data[..4]);
        if (identifier is null)
        {
            return null;
        }
        var board = new BetaflightBoardInfo(identifier, BinaryPrimitives.ReadUInt16LittleEndian(data[4..]));
        if (data.Length == 6)
        {
            return board;
        }
        board = board with { BoardType = data[6] };
        if (data.Length == 7 || api < new Version(1, 37))
        {
            return board;
        }
        board = board with { Capabilities = (BetaflightTargetCapabilities)data[7] };
        var offset = 8;
        if (offset == data.Length)
        {
            return board;
        }
        if (!ReadString(data, ref offset, out var target))
        {
            return null;
        }
        board = board with { TargetName = target };
        if (offset == data.Length || api < new Version(1, 39))
        {
            return board;
        }
        if (!ReadString(data, ref offset, out var name) || !ReadString(data, ref offset, out var manufacturer))
        {
            return null;
        }
        board = board with { BoardName = name, ManufacturerId = manufacturer };
        if (offset == data.Length)
        {
            return board;
        }
        if (data.Length - offset < 32)
        {
            return null;
        }
        board = board with { Signature = Convert.ToHexString(data.Slice(offset, 32)) };
        offset += 32;
        if (offset < data.Length)
        {
            board = board with { McuId = data[offset++] };
        }
        if (offset < data.Length && api >= new Version(1, 42))
        {
            board = board with { ConfigurationState = data[offset] };
        }
        return board;
    }

    /// <summary>Decodes printable ASCII, preserving unknown identifiers without replacement characters.</summary>
    public static string? Text(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return null;
        }
        foreach (var value in data)
        {
            if (value < 32 || value > 126)
            {
                return null;
            }
        }
        return Encoding.ASCII.GetString(data);
    }

    /// <summary>Reads a length-prefixed optional string without reading past the response.</summary>
    public static bool ReadString(ReadOnlySpan<byte> data, ref int offset, out string? text)
    {
        text = null;
        if (offset < 0 || offset >= data.Length)
        {
            return false;
        }
        var length = data[offset++];
        if (length > data.Length - offset)
        {
            return false;
        }
        text = Text(data.Slice(offset, length));
        offset += length;
        return length == 0 || text is not null;
    }
}
