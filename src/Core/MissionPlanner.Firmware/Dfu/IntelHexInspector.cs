using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace MissionPlanner.Firmware.Dfu;

/// <summary>Performs bounded Intel HEX parsing and conservative STM32 address-policy inspection.</summary>
public sealed class IntelHexInspector(IOptions<DfuOptions> options, TimeProvider timeProvider) : IIntelHexInspector
{
    private readonly DfuOptions configured = options.Value;

    /// <inheritdoc />
    public async Task<DfuArtifactMetadata> InspectAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ValidateOptions();

        var source = await ReadBoundedAsync(stream, configured.MaximumIntelHexSourceBytes, cancellationToken).ConfigureAwait(false);
        if (source.Length == 0) throw new InvalidDataException("The Intel HEX file is empty.");
        if (source.Any(value => value > 0x7f)) throw new InvalidDataException("Intel HEX input must contain ASCII text only.");

        var sha256 = Convert.ToHexString(SHA256.HashData(source));
        var lines = Encoding.ASCII.GetString(source).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var data = new SortedDictionary<uint, byte>();
        var seenRecords = new HashSet<string>(StringComparer.Ordinal);
        var warnings = new List<string>();
        ulong addressBase = 0;
        uint? entryAddress = null;
        var eofSeen = false;
        var duplicateWarningAdded = false;

        for (var index = 0; index < lines.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = lines[index].TrimEnd('\r');
            if (line.Length == 0 && index == lines.Length - 1) continue;
            if (line.Length == 0) throw Error(index, "Blank records are not permitted.");
            if (eofSeen) throw Error(index, "Records are not permitted after EOF.");
            if (!seenRecords.Add(line) && !duplicateWarningAdded)
            {
                warnings.Add("The file contains one or more duplicate records.");
                duplicateWarningAdded = true;
            }

            var record = ParseRecord(line, index);
            var count = record[0];
            var address = (ushort)((record[1] << 8) | record[2]);
            var type = record[3];
            var payload = record.AsSpan(4, count);

            switch (type)
            {
                case 0x00:
                    AddData(data, addressBase, address, payload, index, warnings);
                    break;
                case 0x01:
                    RequireShape(address, count, index, "EOF", 0);
                    eofSeen = true;
                    break;
                case 0x02:
                    RequireShape(address, count, index, "extended segment address", 2);
                    addressBase = (ulong)((payload[0] << 8) | payload[1]) << 4;
                    break;
                case 0x03:
                    RequireShape(address, count, index, "start segment address", 4);
                    SetEntryAddress(ref entryAddress, (uint)(((payload[0] << 8) | payload[1]) * 16 + ((payload[2] << 8) | payload[3])), index, warnings);
                    break;
                case 0x04:
                    RequireShape(address, count, index, "extended linear address", 2);
                    addressBase = (ulong)((payload[0] << 8) | payload[1]) << 16;
                    break;
                case 0x05:
                    RequireShape(address, count, index, "start linear address", 4);
                    SetEntryAddress(ref entryAddress, ((uint)payload[0] << 24) | ((uint)payload[1] << 16) | ((uint)payload[2] << 8) | payload[3], index, warnings);
                    break;
                default:
                    throw Error(index, $"Unsupported record type {type:X2}.");
            }
        }

        if (!eofSeen) throw new InvalidDataException("The Intel HEX file has no EOF record.");
        if (data.Count == 0) throw new InvalidDataException("The Intel HEX file contains no data records.");

        var lowest = data.First().Key;
        var highest = data.Last().Key;
        var span = (ulong)highest - lowest + 1;
        if (span > (ulong)configured.MaximumIntelHexAddressSpan)
            throw new InvalidDataException($"The represented address span exceeds {configured.MaximumIntelHexAddressSpan} bytes.");

        var hasBootloaderEvidence = data.Keys.Any(address => address < configured.ExpectedApplicationStartAddress);
        var hasApplicationEvidence = data.Keys.Any(address => address >= configured.ExpectedApplicationStartAddress);
        if (!hasBootloaderEvidence) warnings.Add("No data was found in the configured bootloader evidence region.");
        if (!hasApplicationEvidence) warnings.Add("No data was found in the configured application evidence region.");

        return new DfuArtifactMetadata(
            source.LongLength,
            data.Count,
            lowest,
            highest,
            sha256,
            MergeRanges(data),
            warnings.AsReadOnly(),
            entryAddress,
            hasBootloaderEvidence,
            timeProvider.GetUtcNow(),
            hasApplicationEvidence);
    }

    private void AddData(SortedDictionary<uint, byte> data, ulong addressBase, ushort recordAddress, ReadOnlySpan<byte> payload, int lineIndex, List<string> warnings)
    {
        if (payload.IsEmpty) return;
        var start = addressBase + recordAddress;
        var end = start + (ulong)payload.Length - 1;
        if (start > uint.MaxValue || end > uint.MaxValue) throw Error(lineIndex, "Data address calculation overflowed the 32-bit address space.");
        if (start < configured.Stm32FlashStartAddress || end >= configured.Stm32FlashEndAddressExclusive)
            throw Error(lineIndex, "Data lies outside the configured STM32 internal-flash address policy.");

        var identicalOverlap = false;
        for (var offset = 0; offset < payload.Length; offset++)
        {
            var address = (uint)(start + (uint)offset);
            if (data.TryGetValue(address, out var existing))
            {
                if (existing != payload[offset]) throw Error(lineIndex, $"Data conflicts with an earlier record at 0x{address:X8}.");
                identicalOverlap = true;
                continue;
            }

            data.Add(address, payload[offset]);
            if (data.Count > configured.MaximumIntelHexDataBytes)
                throw Error(lineIndex, $"Unique data exceeds {configured.MaximumIntelHexDataBytes} bytes.");
        }
        if (identicalOverlap) warnings.Add($"Line {lineIndex + 1} overlaps identical data from an earlier record.");
    }

    private static byte[] ParseRecord(string line, int lineIndex)
    {
        if (!line.StartsWith(':')) throw Error(lineIndex, "A record must begin with ':'.");
        var encoded = line.AsSpan(1);
        if (encoded.Length < 10 || encoded.Length % 2 != 0) throw Error(lineIndex, "The record has an invalid encoded length.");
        byte[] record;
        try { record = Convert.FromHexString(encoded); }
        catch (FormatException exception) { throw Error(lineIndex, "The record contains invalid hexadecimal text.", exception); }
        if (record.Length != record[0] + 5) throw Error(lineIndex, "The byte count does not match the record length.");
        if (record.Aggregate(0, (sum, value) => sum + value) % 256 != 0) throw Error(lineIndex, "The record checksum is invalid.");
        return record;
    }

    private static void RequireShape(ushort address, byte count, int lineIndex, string name, byte expectedCount)
    {
        if (address != 0 || count != expectedCount) throw Error(lineIndex, $"The {name} record has an invalid address or byte count.");
    }

    private static void SetEntryAddress(ref uint? entryAddress, uint candidate, int lineIndex, List<string> warnings)
    {
        if (entryAddress is uint existing && existing != candidate) throw Error(lineIndex, "Start-address records conflict.");
        if (entryAddress == candidate) warnings.Add($"Line {lineIndex + 1} repeats the start address.");
        entryAddress = candidate;
    }

    private static IReadOnlyList<DfuMemoryRange> MergeRanges(SortedDictionary<uint, byte> data)
    {
        var result = new List<DfuMemoryRange>();
        uint? start = null;
        uint previous = 0;
        var bytes = new List<byte>();
        foreach (var item in data)
        {
            if (start is not null && item.Key != (ulong)previous + 1)
            {
                result.Add(new DfuMemoryRange(start.Value, bytes.ToArray()));
                bytes.Clear();
                start = null;
            }
            start ??= item.Key;
            previous = item.Key;
            bytes.Add(item.Value);
        }
        result.Add(new DfuMemoryRange(start!.Value, bytes.ToArray()));
        return result.AsReadOnly();
    }

    private void ValidateOptions()
    {
        if (configured.MaximumIntelHexSourceBytes <= 0 || configured.MaximumIntelHexDataBytes <= 0 || configured.MaximumIntelHexAddressSpan <= 0)
            throw new InvalidOperationException("DFU Intel HEX limits must be positive.");
        if (configured.Stm32FlashEndAddressExclusive <= configured.Stm32FlashStartAddress)
            throw new InvalidOperationException("The STM32 flash policy range is invalid.");
        if (configured.ExpectedApplicationStartAddress <= configured.Stm32FlashStartAddress || configured.ExpectedApplicationStartAddress >= configured.Stm32FlashEndAddressExclusive)
            throw new InvalidOperationException("The expected application address must lie inside the STM32 flash policy range.");
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream stream, long maximumBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0) return buffer.ToArray();
            if (buffer.Length + read > maximumBytes) throw new InvalidDataException($"The Intel HEX source exceeds {maximumBytes} bytes.");
            buffer.Write(chunk, 0, read);
        }
    }

    private static InvalidDataException Error(int lineIndex, string message, Exception? inner = null) =>
        new($"Intel HEX line {lineIndex + 1}: {message}", inner);
}
