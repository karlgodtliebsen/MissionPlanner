using System.Buffers.Binary;

namespace MissionPlanner.Core.Replay;

/// <summary>Reads Mission Planner telemetry logs containing an eight-byte timestamp before each MAVLink frame.</summary>
public sealed class TelemetryLogReader : ITelemetryLogReader
{
    private const byte MavLinkV1Magic = 0xFE;
    private const byte MavLinkV2Magic = 0xFD;
    private const int TimestampLength = 8;
    private const int V1PacketOverhead = 8;
    private const int V2PacketOverhead = 12;
    private const int SignatureLength = 13;
    private const byte SignedFlag = 0x01;
    private static readonly ulong maxUnixMicroseconds = (ulong)((DateTimeOffset.MaxValue - DateTimeOffset.UnixEpoch).Ticks / 10);

    /// <inheritdoc />
    public async Task<TelemetryLogIndex> IndexAsync(
        Stream stream,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        ValidateStream(stream);
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            throw new ArgumentException("A telemetry-log source name is required.", nameof(sourceName));
        }

        stream.Position = 0;
        var entries = new List<TelemetryLogIndexEntry>();
        var timestampBytes = new byte[TimestampLength];
        var packetPrefix = new byte[3];
        DateTimeOffset? previousTimestamp = null;
        var adjustedTimestampCount = 0;

        while (stream.Position < stream.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timestampOffset = stream.Position;
            await ReadExactlyAsync(stream, timestampBytes, cancellationToken).ConfigureAwait(false);
            var timestamp = DecodeTimestamp(timestampBytes, timestampOffset);
            if (previousTimestamp is { } previous && timestamp < previous)
            {
                timestamp = previous;
                adjustedTimestampCount++;
            }

            var packetOffset = stream.Position;
            await ReadExactlyAsync(stream, packetPrefix, cancellationToken).ConfigureAwait(false);
            var packetLength = GetPacketLength(packetPrefix, packetOffset);
            var packetEnd = checked(packetOffset + packetLength);
            if (packetEnd > stream.Length)
            {
                throw new InvalidDataException(
                    $"Telemetry log ends inside MAVLink packet {entries.Count} at byte {packetOffset}.");
            }

            entries.Add(new TelemetryLogIndexEntry(
                entries.Count,
                timestampOffset,
                packetOffset,
                packetLength,
                timestamp));
            stream.Position = packetEnd;
            previousTimestamp = timestamp;
        }

        return new TelemetryLogIndex(
            sourceName.Trim(),
            stream.Length,
            entries,
            entries.Count == 0 ? null : entries[0].Timestamp,
            entries.Count == 0 ? null : entries[^1].Timestamp,
            adjustedTimestampCount);
    }

    /// <inheritdoc />
    public async Task<TelemetryLogRecord> ReadAsync(
        Stream stream,
        TelemetryLogIndexEntry entry,
        CancellationToken cancellationToken = default)
    {
        ValidateStream(stream);
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.PacketOffset < 0 || entry.PacketLength <= 0 || entry.PacketOffset + entry.PacketLength > stream.Length)
        {
            throw new InvalidDataException("The telemetry-log index entry is outside the supplied stream.");
        }

        stream.Position = entry.PacketOffset;
        var packet = new byte[entry.PacketLength];
        await ReadExactlyAsync(stream, packet, cancellationToken).ConfigureAwait(false);
        return new TelemetryLogRecord(entry, packet);
    }

    private static void ValidateStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new ArgumentException("Telemetry-log playback requires a readable, seekable stream.", nameof(stream));
        }
    }

    private static DateTimeOffset DecodeTimestamp(ReadOnlySpan<byte> bytes, long offset)
    {
        var microseconds = BinaryPrimitives.ReadUInt64BigEndian(bytes);
        if (microseconds > maxUnixMicroseconds)
        {
            throw new InvalidDataException($"Telemetry-log timestamp at byte {offset} is outside the supported UTC range.");
        }

        return DateTimeOffset.UnixEpoch.AddTicks(checked((long)microseconds * 10));
    }

    private static int GetPacketLength(ReadOnlySpan<byte> prefix, long offset)
    {
        var payloadLength = prefix[1];
        return prefix[0] switch
        {
            MavLinkV1Magic => payloadLength + V1PacketOverhead,
            MavLinkV2Magic => payloadLength + V2PacketOverhead +
                              ((prefix[2] & SignedFlag) != 0 ? SignatureLength : 0),
            _ => throw new InvalidDataException(
                $"Expected a MAVLink v1 or v2 frame at telemetry-log byte {offset}, but found 0x{prefix[0]:X2}.")
        };
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        try
        {
            await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("Telemetry log contains a truncated timestamp or MAVLink packet header.", exception);
        }
    }
}
