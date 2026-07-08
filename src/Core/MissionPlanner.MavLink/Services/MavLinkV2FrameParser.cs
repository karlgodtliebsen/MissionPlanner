using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Services;

/// <summary>
/// Parser for MAVLink v2 frames.
/// </summary>
public sealed class MavLinkV2FrameParser : IMavLinkFrameParser
{
    private const byte MavLinkV2Magic = 0xFD;
    private const int HeaderLength = 10;
    private const int ChecksumLength = 2;
    private const int SignatureLength = 13;

    private readonly List<byte> buffer = [];
    private readonly object bufferLock = new();
    private readonly IMavLinkCrcExtraProvider crcExtraProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MavLinkV2FrameParser"/> class.
    /// </summary>
    /// <param name="crcExtraProvider">The CRC extra provider.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="crcExtraProvider"/> is null.</exception>
    public MavLinkV2FrameParser(IMavLinkCrcExtraProvider crcExtraProvider)
    {
        this.crcExtraProvider = crcExtraProvider ?? throw new ArgumentNullException(nameof(crcExtraProvider));
    }

    /// <inheritdoc />
    public IReadOnlyList<MavLinkFrame> Parse(ReadOnlySpan<byte> data, TransportEndPoint endPoint, DateTimeOffset receivedAt)
    {
        lock (bufferLock)
        {
            foreach (var b in data)
            {
                buffer.Add(b);
            }

            var frames = new List<MavLinkFrame>();

            while (TryReadFrame(endPoint, receivedAt, out var frame))
            {
                if (frame is not null)
                {
                    frames.Add(frame);
                }
            }

            return frames;
        }
    }

    private bool TryReadFrame(TransportEndPoint endPoint, DateTimeOffset receivedAt, out MavLinkFrame? frame)
    {
        // Note: This method is called from within Parse() which already holds bufferLock
        frame = null;

        while (buffer.Count > 0 && buffer[0] != MavLinkV2Magic)
        {
            buffer.RemoveAt(0);
        }

        if (buffer.Count < HeaderLength)
        {
            return false;
        }

        var payloadLength = buffer[1];
        var incompatFlags = buffer[2];
        var isSigned = (incompatFlags & 0x01) != 0;

        var frameLength = HeaderLength + payloadLength + ChecksumLength + (isSigned ? SignatureLength : 0);

        if (buffer.Count < frameLength)
        {
            return false;
        }

        // Defensive: Ensure we don't try to remove more bytes than available
        var bytesToRemove = Math.Min(frameLength, buffer.Count);

        // If we can't get a complete frame, skip the magic byte and try again
        if (bytesToRemove < frameLength)
        {
            buffer.RemoveAt(0);
            return true;
        }

        // Extra defensive check: verify buffer still has enough bytes
        if (bytesToRemove > buffer.Count || bytesToRemove <= 0)
        {
            // Buffer state changed unexpectedly, skip magic byte
            if (buffer.Count > 0)
            {
                buffer.RemoveAt(0);
            }
            return true;
        }

        var rawBytes = buffer.GetRange(0, bytesToRemove).ToArray();

        // Final check before RemoveRange
        if (bytesToRemove > buffer.Count)
        {
            return true;
        }

        buffer.RemoveRange(0, bytesToRemove);

        var sequence = rawBytes[4];
        var systemId = rawBytes[5];
        var componentId = rawBytes[6];

        var messageId = rawBytes[7] | ((uint)rawBytes[8] << 8) | ((uint)rawBytes[9] << 16);

        if (!crcExtraProvider.TryGetCrcExtra(messageId, out var crcExtra))
        {
            // Unknown message id for current dialect/provider.
            // Frame boundary was valid, but we cannot validate CRC.
            // Skip frame and keep parsing subsequent buffered data.
            return true;
        }

        var receivedCrcOffset = HeaderLength + payloadLength;

        var receivedCrc = (ushort)(rawBytes[receivedCrcOffset] | (rawBytes[receivedCrcOffset + 1] << 8));

        var calculatedCrc = MavLinkCrc.Calculate(rawBytes.AsSpan(1, HeaderLength - 1 + payloadLength), crcExtra);

        if (receivedCrc != calculatedCrc)
        {
            // Invalid frame. Skip it and keep parsing subsequent data.
            return true;
        }

        var payload = rawBytes.AsMemory(HeaderLength, payloadLength).ToArray();

        frame = new MavLinkFrame(systemId, componentId, endPoint, messageId, sequence, payload, rawBytes, receivedAt);

        return true;
    }
}
