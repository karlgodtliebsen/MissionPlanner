using Microsoft.Extensions.Logging;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Services;

/// <summary>
/// Stateful parser for MAVLink v1 and v2 frames.
/// This parser accepts arbitrary byte chunks: one chunk may contain partial frames, one frame, or many frames.
/// </summary>
public sealed class MavLinkV2FrameParser : IMavLinkFrameParser
{
    private const byte MavLinkV1Magic = 0xFE;
    private const byte MavLinkV2Magic = 0xFD;
    private const int V1HeaderLength = 6;
    private const int V2HeaderLength = 10;
    private const int ChecksumLength = 2;
    private const int SignatureLength = 13;
    private const int MaxPayloadLength = 255;
    private const int MaxFrameLength = V2HeaderLength + MaxPayloadLength + ChecksumLength + SignatureLength;
    private const int CompactThreshold = 4096;

    private readonly IMavLinkMessageDefinitionRegistry messageDefinitions;
    private readonly ILogger<MavLinkV2FrameParser>? logger;
    private readonly object syncRoot = new();
    private readonly List<byte> buffer = new(8192);

    private int readOffset;

    /// <summary>
    /// Initializes a new instance of the <see cref="MavLinkV2FrameParser"/> class.
    /// </summary>
    /// <param name="messageDefinitions">The generated MAVLink message-definition registry.</param>
    /// <param name="logger">The logger.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public MavLinkV2FrameParser(
        IMavLinkMessageDefinitionRegistry messageDefinitions,
        ILogger<MavLinkV2FrameParser>? logger = null)
    {
        this.messageDefinitions = messageDefinitions ?? throw new ArgumentNullException(nameof(messageDefinitions));
        this.logger = logger;
    }

    /// <summary>
    /// Parses the given byte span into MAVLink frames.
    /// </summary>
    /// <param name="data">The byte span containing MAVLink data.</param>
    /// <param name="endPoint">The transport endpoint from which the data was received.</param>
    /// <param name="receivedAt">The timestamp when the data was received.</param>
    /// <returns>A list of parsed MAVLink frames.</returns>
    public IReadOnlyList<MavLinkFrame> Parse(ReadOnlySpan<byte> data, TransportEndPoint endPoint, DateTimeOffset receivedAt)
    {
        if (data.IsEmpty)
        {
            return Array.Empty<MavLinkFrame>();
        }

        lock (syncRoot)
        {
            Append(data);

            var frames = new List<MavLinkFrame>();

            while (TryReadFrame(endPoint, receivedAt, out var frame))
            {
                if (frame is not null)
                {
                    frames.Add(frame);
                }
            }

            CompactIfNeeded();
            return frames;
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (syncRoot)
        {
            buffer.Clear();
            readOffset = 0;
        }
    }

    private void Append(ReadOnlySpan<byte> data)
    {
        buffer.EnsureCapacity(buffer.Count + data.Length);

        foreach (var b in data)
        {
            buffer.Add(b);
        }
    }

    private bool TryReadFrame(TransportEndPoint endPoint, DateTimeOffset receivedAt, out MavLinkFrame? frame)
    {
        frame = null;

        SkipUntilMagic();

        if (AvailableBytes < 2)
        {
            return false;
        }

        var isV2 = buffer[readOffset] == MavLinkV2Magic;
        var headerLength = isV2 ? V2HeaderLength : V1HeaderLength;
        if (AvailableBytes < headerLength)
        {
            return false;
        }

        var payloadLength = buffer[readOffset + 1];
        var incompatFlags = isV2 ? buffer[readOffset + 2] : (byte)0;
        var isSigned = isV2 && (incompatFlags & 0x01) != 0;
        var frameLength = headerLength + payloadLength + ChecksumLength + (isSigned ? SignatureLength : 0);

        if (frameLength < headerLength + ChecksumLength || frameLength > MaxFrameLength)
        {
            // Impossible MAVLink v2 frame. Drop this magic byte and search again.
            readOffset++;
            return true;
        }

        if (AvailableBytes < frameLength)
        {
            return false;
        }

        var rawBytes = new byte[frameLength];
        CopyFromBuffer(readOffset, rawBytes);

        var sequence = rawBytes[isV2 ? 4 : 2];
        var systemId = rawBytes[isV2 ? 5 : 3];
        var componentId = rawBytes[isV2 ? 6 : 4];
        var messageId = isV2
            ? rawBytes[7] | ((uint)rawBytes[8] << 8) | ((uint)rawBytes[9] << 16)
            : rawBytes[5];

        if (!messageDefinitions.TryGet(messageId, out var definition))
        {
            if (logger?.IsEnabled(LogLevel.Trace) == true)
            {
                logger.LogTrace("Skipping MAVLink frame for unknown message. MessageId={MessageId}", messageId);
            }

            // An unknown ID normally means an unsupported dialect. It can also mean we locked onto a false 0xFD byte.
            // Drop only the magic byte so the parser can quickly resynchronise instead of discarding a large
            // candidate frame that may contain valid frames after the false magic byte.
            readOffset++;
            return true;
        }

        var validPayloadLength = isV2
            ? payloadLength >= definition.MinimumPayloadLength && payloadLength <= definition.MaximumPayloadLength
            : payloadLength == definition.MinimumPayloadLength;
        if (!validPayloadLength)
        {
            if (logger?.IsEnabled(LogLevel.Debug) == true)
            {
                logger.LogDebug(
                    "Skipping MAVLink frame with invalid payload length. MessageId={MessageId}, MessageName={MessageName}, PayloadLength={PayloadLength}, MinimumPayloadLength={MinimumPayloadLength}, MaximumPayloadLength={MaximumPayloadLength}",
                    messageId,
                    definition.Name,
                    payloadLength,
                    definition.MinimumPayloadLength,
                    definition.MaximumPayloadLength);
            }

            readOffset++;
            return true;
        }

        var receivedCrcOffset = headerLength + payloadLength;
        var receivedCrc = (ushort)(rawBytes[receivedCrcOffset] | (rawBytes[receivedCrcOffset + 1] << 8));
        var calculatedCrc = MavLinkCrc.Calculate(rawBytes.AsSpan(1, headerLength - 1 + payloadLength), definition.CrcExtra);

        if (receivedCrc != calculatedCrc)
        {
            if (logger?.IsEnabled(LogLevel.Debug) == true)
            {
                logger.LogDebug("Skipping MAVLink frame with invalid CRC. MessageId={MessageId}, MessageName={MessageName}, ReceivedCrc={ReceivedCrc}, CalculatedCrc={CalculatedCrc}",
                    messageId,
                    definition.Name,
                    receivedCrc,
                    calculatedCrc);
            }

            // CRC failure means either a damaged frame or a false 0xFD in the stream. Drop only the magic
            // byte and rescan; consuming the whole candidate frame can skip valid frames.
            readOffset++;
            return true;
        }

        // Valid frame. Consume the full frame only after CRC validation.
        readOffset += frameLength;

        var payload = new byte[payloadLength];
        rawBytes.AsSpan(headerLength, payloadLength).CopyTo(payload);

        frame = new MavLinkFrame(systemId, componentId, endPoint, messageId, sequence, payload, rawBytes, receivedAt);
        return true;
    }

    private int AvailableBytes => buffer.Count - readOffset;

    private void SkipUntilMagic()
    {
        while (AvailableBytes > 0 && buffer[readOffset] is not MavLinkV1Magic and not MavLinkV2Magic)
        {
            readOffset++;
        }
    }

    private void CopyFromBuffer(int offset, Span<byte> destination)
    {
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = buffer[offset + i];
        }
    }

    private void CompactIfNeeded()
    {
        if (readOffset == 0)
        {
            return;
        }

        if (readOffset == buffer.Count)
        {
            buffer.Clear();
            readOffset = 0;
            return;
        }

        if (readOffset < CompactThreshold && readOffset < buffer.Count / 2)
        {
            return;
        }

        buffer.RemoveRange(0, readOffset);
        readOffset = 0;
    }
}
