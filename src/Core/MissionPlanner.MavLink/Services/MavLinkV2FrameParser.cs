using Microsoft.Extensions.Logging;
using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Services;

/// <summary>
/// Stateful parser for MAVLink v2 frames.
/// This parser accepts arbitrary byte chunks: one chunk may contain partial frames, one frame, or many frames.
/// </summary>
public sealed class MavLinkV2FrameParser : IMavLinkFrameParser
{
    private const byte MavLinkV2Magic = 0xFD;
    private const int HeaderLength = 10;
    private const int ChecksumLength = 2;
    private const int SignatureLength = 13;
    private const int MaxPayloadLength = 255;
    private const int MaxFrameLength = HeaderLength + MaxPayloadLength + ChecksumLength + SignatureLength;
    private const int CompactThreshold = 4096;

    private readonly IMavLinkCrcExtraProvider crcExtraProvider;
    private readonly ILogger<MavLinkV2FrameParser>? logger;
    private readonly object syncRoot = new();
    private readonly List<byte> buffer = new(8192);

    private int readOffset;

    /// <summary>
    /// Initializes a new instance of the <see cref="MavLinkV2FrameParser"/> class.
    /// </summary>
    /// <param name="crcExtraProvider">The CRC extra provider.</param>
    /// <param name="logger">The logger.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public MavLinkV2FrameParser(IMavLinkCrcExtraProvider crcExtraProvider, ILogger<MavLinkV2FrameParser>? logger = null)
    {
        this.crcExtraProvider = crcExtraProvider ?? throw new ArgumentNullException(nameof(crcExtraProvider));
        this.logger = logger;
    }

    /// <summary>
    /// Parses the given byte span into MAVLink frames.
    /// </summary>
    /// <param name="data">The byte span containing MAVLink data.</param>
    /// <param name="endPoint">The transport endpoint from which the data was received.</param>
    /// <param name="receivedAt">The timestamp when the data was received.</param>
    /// <returns>A list of parsed MAVLink frames.</returns>
    public IReadOnlyList<MavLinkFrame> Parse(ReadOnlySpan<byte> data, TransportEndPoint? endPoint, DateTimeOffset receivedAt)
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

    private void Append(ReadOnlySpan<byte> data)
    {
        buffer.EnsureCapacity(buffer.Count + data.Length);

        foreach (var b in data)
        {
            buffer.Add(b);
        }
    }

    private bool TryReadFrame(TransportEndPoint? endPoint, DateTimeOffset receivedAt, out MavLinkFrame? frame)
    {
        frame = null;

        SkipUntilMagic();

        if (AvailableBytes < HeaderLength)
        {
            return false;
        }

        var payloadLength = buffer[readOffset + 1];
        var incompatFlags = buffer[readOffset + 2];
        var isSigned = (incompatFlags & 0x01) != 0;
        var frameLength = HeaderLength + payloadLength + ChecksumLength + (isSigned ? SignatureLength : 0);

        if (frameLength is < HeaderLength + ChecksumLength or > MaxFrameLength)
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

        var sequence = rawBytes[4];
        var systemId = rawBytes[5];
        var componentId = rawBytes[6];
        var messageId = rawBytes[7] | ((uint)rawBytes[8] << 8) | ((uint)rawBytes[9] << 16);

        if (!crcExtraProvider.TryGetCrcExtra(messageId, out var crcExtra))
        {
            if (logger?.IsEnabled(LogLevel.Trace) == true)
            {
                logger.LogTrace("Skipping MAVLink frame with unknown CRC extra. MessageId={MessageId}", messageId);
            }

            // Unknown CRC normally means unsupported dialect. It can also mean we locked onto a false 0xFD byte.
            // Drop only the magic byte so the parser can quickly resynchronise instead of discarding a large
            // candidate frame that may contain valid frames after the false magic byte.
            readOffset++;
            return true;
        }

        var receivedCrcOffset = HeaderLength + payloadLength;
        var receivedCrc = (ushort)(rawBytes[receivedCrcOffset] | (rawBytes[receivedCrcOffset + 1] << 8));
        var calculatedCrc = MavLinkCrc.Calculate(rawBytes.AsSpan(1, HeaderLength - 1 + payloadLength), crcExtra);

        if (receivedCrc != calculatedCrc)
        {
            if (logger?.IsEnabled(LogLevel.Debug) == true)
            {
                logger.LogDebug("Skipping MAVLink frame with invalid CRC. MessageId={MessageId}, ReceivedCrc={ReceivedCrc}, CalculatedCrc={CalculatedCrc}",
                    messageId,
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
        rawBytes.AsSpan(HeaderLength, payloadLength).CopyTo(payload);

        frame = new MavLinkFrame(systemId, componentId, endPoint, messageId, sequence, payload, rawBytes, receivedAt);
        return true;
    }

    private int AvailableBytes => buffer.Count - readOffset;

    private void SkipUntilMagic()
    {
        while (AvailableBytes > 0 && buffer[readOffset] != MavLinkV2Magic)
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
