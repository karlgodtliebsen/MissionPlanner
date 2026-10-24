using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>One explicitly supported bootloader/board combination, not a generic radio-flashing target.</summary>
public sealed record SikFirmwareBoard(byte Id, string Name, bool Banked, bool XSeries, string ChipId, string ImageToken)
{
    /// <summary>Gets supported classic SiK and RFD X-series identities.</summary>
    public static IReadOnlyList<SikFirmwareBoard> Supported { get; } =
    [
        new(0x4D, "RF50 demo", false, false, "", "RF50_DEMO"),
        new(0x4E, "HM-TRP / compatible SiK", false, false, "", "HM-TRP"),
        new(0x4F, "ISM01A", false, false, "", "ISM01A"),
        new(0x50, "HB1060", false, false, "", "HB1060"),
        new(0x51, "3DR1060", false, false, "", "3DR1060"),
        new(0x42, "RFD900", false, false, "", "RFD900"),
        new(0x43, "RFD900A", false, false, "", "RFD900A"),
        new(0x81, "RFD900U", true, false, "", "RFD900U"),
        new(0x82, "RFD900P", true, false, "", "RFD900P"),
        new(0x91, "mRo900", true, false, "", "MRO900"),
        new(0x83, "RFD900x revision 1", false, true, "RFD900xSub:1", "RFD900x"),
        new(0x88, "RFD900ux revision 1", false, true, "RFD900uxSub:1", "RFD900ux"),
        new(0x84, "RFD900x revision 2", false, true, "RFD900xSub:2", ""),
        new(0x85, "RFD900ux revision 2", false, true, "RFD900uxSub:2", "")
    ];
    /// <summary>Gets the classic application-valid signature location.</summary>
    public uint SignatureAddress => Banked ? 0x3F7FEu : 0xF7FEu;
    /// <summary>Checks classic application address visibility without allowing bootloader, calibration-storage or parameter-page writes.</summary>
    public bool CanWrite(uint address)
    {
        if (XSeries) return false;
        if (!Banked) return address is >= 0x400 and < 0xF800;
        var bank = address >> 16; var offset = address & 0xFFFF;
        return bank switch { 0 => offset is >= 0x400 and < 0x8000, 1 or 2 => offset >= 0x8000, 3 => offset is >= 0x8000 and < 0xF800, _ => false };
    }
}

/// <summary>A bounded local image with validated record checksums, board marker and flash-address policy.</summary>
public sealed class SikFirmwareImage
{
    private SikFirmwareImage(string name, SikFirmwareBoard board, byte[] source, SortedDictionary<uint, byte> bytes)
    {
        Name = name; Board = board; Source = source; Bytes = new ReadOnlyDictionary<uint, byte>(bytes); Sha256 = Convert.ToHexString(SHA256.HashData(source));
    }
    /// <summary>Gets the selected file's display name.</summary>
    public string Name { get; }
    /// <summary>Gets the explicitly selected target board profile.</summary>
    public SikFirmwareBoard Board { get; }
    /// <summary>Gets the SHA-256 of the exact selected source bytes.</summary>
    public string Sha256 { get; }
    /// <summary>Gets classic flash addresses and bytes, excluding no supplied application data.</summary>
    public IReadOnlyDictionary<uint, byte> Bytes { get; }
    /// <summary>Gets original file bytes; X-series images are transmitted without reinterpretation.</summary>
    public ReadOnlyMemory<byte> Source { get; }
    /// <summary>Checks a local image before any port is opened; an embedded board marker is necessary but not proof of image compatibility.</summary>
    public static SikFirmwareImage Parse(string name, ReadOnlySpan<byte> source, SikFirmwareBoard board)
    {
        if (!SikFirmwareBoard.Supported.Contains(board) || source.Length is 0 or > 1024 * 1024) throw new ArgumentException("Select a supported radio board and a nonempty image up to 1 MiB.");
        var bytes = new SortedDictionary<uint, byte>();
        if (board.XSeries)
        {
            if (!name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) || source.Length < 256 || board.ImageToken.Length > 0 && !HasMarker(source, board.ImageToken))
                throw new InvalidDataException("Select an RFD X-series .bin containing the matching board marker. Confirm hardware revision and firmware compatibility separately.");
            if (board.ChipId.EndsWith(":1", StringComparison.Ordinal) && !Encoding.ASCII.GetString(source).Contains("HastaLaVistaBaby", StringComparison.Ordinal))
                throw new InvalidDataException("Revision-1 RFD X-series updates require a certified firmware marker, preserving compatibility with country-locked radios. No lock bypass or temporary reference image is used.");
        }
        else
        {
            if (!(name.EndsWith(".ihx", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".hex", StringComparison.OrdinalIgnoreCase)) || source.ContainsAnyExceptInRange((byte)0, (byte)127))
                throw new InvalidDataException("Classic SiK requires an ASCII Intel HEX .ihx/.hex image.");
            uint upper = 0; var eof = false;
            foreach (var raw in Encoding.ASCII.GetString(source).Split('\n'))
            {
                var line = raw.Trim(); if (line.Length == 0) continue;
                if (eof || line.Length < 11 || line.Length > 521 || line[0] != ':' || line.Length % 2 != 1) throw new InvalidDataException("Malformed Intel HEX line or data after EOF.");
                byte[] record;
                try { record = Convert.FromHexString(line[1..]); }
                catch (FormatException ex) { throw new InvalidDataException("Invalid Intel HEX digit.", ex); }
                var count = record[0]; var offset = (uint)(record[1] << 8 | record[2]);
                if (record.Length != count + 5 || (record.Sum(value => value) & 255) != 0) throw new InvalidDataException("Intel HEX length/checksum mismatch.");
                switch (record[3])
                {
                    case 0:
                        if (count == 0 || offset + count > 65536) throw new InvalidDataException("Invalid or bank-crossing data record.");
                        for (var i = 0; i < count; i++)
                        {
                            var address = upper + offset + (uint)i;
                            if (!board.CanWrite(address) || !bytes.TryAdd(address, record[4 + i])) throw new InvalidDataException("Image overlaps data or addresses reserved flash/unsupported banks.");
                        }
                        break;
                    case 1 when count == 0 && offset == 0: eof = true; break;
                    case 4 when count == 2 && offset == 0:
                        upper = (uint)(record[4] << 8 | record[5]) << 16;
                        if (upper > (board.Banked ? 0x30000 : 0)) throw new InvalidDataException("Image uses an unsupported flash bank.");
                        break;
                    default: throw new InvalidDataException("Unsupported Intel HEX record; only data, extended linear address and EOF are accepted for SiK.");
                }
            }
            if (!eof || bytes.Count == 0 || bytes.GetValueOrDefault(board.SignatureAddress) != 0x3D || bytes.GetValueOrDefault(board.SignatureAddress + 1) != 0xC2)
                throw new InvalidDataException("Image is missing EOF, application data or the SiK boot-valid signature.");
            // Do not join gaps when finding an identity marker.
            if (!Chunks(bytes, 1_000_000).Any(chunk => HasMarker(chunk.Data, board.ImageToken))) throw new InvalidDataException("The image does not contain the selected board's identity marker.");
        }
        return new(name, board, source.ToArray(), bytes);
    }
    private static bool HasMarker(ReadOnlySpan<byte> bytes, string marker)
    {
        var text = Encoding.ASCII.GetString(bytes); var start = 0;
        while ((start = text.IndexOf(marker, start, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var end = start + marker.Length;
            if ((start == 0 || !char.IsAsciiLetterOrDigit(text[start - 1])) && (end == text.Length || !char.IsAsciiLetterOrDigit(text[end]))) return true;
            start++;
        }
        return false;
    }
    /// <summary>Produces contiguous, bank-bounded chunks; callers can withhold the validity signature until verification completes.</summary>
    public static IEnumerable<(uint Address, byte[] Data)> Chunks(IEnumerable<KeyValuePair<uint, byte>> source, int maximum)
    {
        if (maximum < 1) throw new ArgumentOutOfRangeException(nameof(maximum));
        uint start = 0; var data = new List<byte>();
        foreach (var pair in source.OrderBy(pair => pair.Key))
        {
            if (data.Count > 0 && (pair.Key != start + data.Count || data.Count >= maximum || pair.Key >> 16 != start >> 16)) { yield return (start, data.ToArray()); data.Clear(); }
            if (data.Count == 0) start = pair.Key;
            data.Add(pair.Value);
        }
        if (data.Count > 0) yield return (start, data.ToArray());
    }
}

/// <summary>Encodes RFD's 128-byte XMODEM-CRC records with explicit block IDs and receiver-compatible padding.</summary>
public static class RfdXmodemCodec
{
    /// <summary>Computes CRC-16/XMODEM, polynomial 0x1021 and initial state zero.</summary>
    public static ushort Crc(ReadOnlySpan<byte> data)
    {
        ushort crc = 0;
        foreach (var value in data) { crc ^= (ushort)(value << 8); for (var bit = 0; bit < 8; bit++) crc = unchecked((ushort)((crc << 1) ^ ((crc & 0x8000) != 0 ? 0x1021 : 0))); }
        return crc;
    }
    /// <summary>Builds a 133-byte SOH block; retries reuse the exact same number and bytes.</summary>
    public static byte[] Block(byte sequence, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is 0 or > 128) throw new ArgumentOutOfRangeException(nameof(bytes));
        var packet = new byte[133]; packet[0] = 1; packet[1] = sequence; packet[2] = (byte)(255 - sequence);
        packet.AsSpan(3, 128).Fill(0x26); bytes.CopyTo(packet.AsSpan(3));
        var crc = Crc(packet.AsSpan(3, 128)); packet[131] = (byte)(crc >> 8); packet[132] = (byte)crc;
        return packet;
    }
}
