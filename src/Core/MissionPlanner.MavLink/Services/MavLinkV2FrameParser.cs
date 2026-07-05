using System.Net;
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
    public IReadOnlyList<MavLinkFrame> Parse(ReadOnlySpan<byte> data, MavLinkEndpoint? remoteEndpoint, DateTimeOffset receivedAt)
    {
        foreach (var b in data) buffer.Add(b);

        var frames = new List<MavLinkFrame>();

        var localAddress = string.IsNullOrWhiteSpace(remoteEndpoint.Address)
            ? IPAddress.Any
            : IPAddress.Parse(remoteEndpoint.Address);
        var ipEndPoint = new IPEndPoint(localAddress, remoteEndpoint.Port ?? 0);

        while (TryReadFrame(ipEndPoint, receivedAt, out var frame))
            if (frame is not null)
                frames.Add(frame);

        return frames;
    }

    /// <inheritdoc />
    public IReadOnlyList<MavLinkFrame> Parse(ReadOnlySpan<byte> data, IPEndPoint ipEndPoint, DateTimeOffset receivedAt)
    {
        foreach (var b in data) buffer.Add(b);

        var frames = new List<MavLinkFrame>();

        while (TryReadFrame(ipEndPoint, receivedAt, out var frame))
            if (frame is not null)
                frames.Add(frame);

        return frames;
    }

    private bool TryReadFrame(IPEndPoint ipEndPoint, DateTimeOffset receivedAt, out MavLinkFrame? frame)
    {
        frame = null;

        while (buffer.Count > 0 && buffer[0] != MavLinkV2Magic) buffer.RemoveAt(0);

        if (buffer.Count < HeaderLength) return false;

        var payloadLength = buffer[1];
        var incompatFlags = buffer[2];
        var isSigned = (incompatFlags & 0x01) != 0;

        var frameLength =
            HeaderLength
            + payloadLength
            + ChecksumLength
            + (isSigned ? SignatureLength : 0);

        if (buffer.Count < frameLength) return false;

        var rawBytes = buffer.Take(frameLength).ToArray();

        buffer.RemoveRange(0, frameLength);

        var sequence = rawBytes[4];
        var systemId = rawBytes[5];
        var componentId = rawBytes[6];

        var messageId = rawBytes[7] | ((uint)rawBytes[8] << 8) | ((uint)rawBytes[9] << 16);

        if (!crcExtraProvider.TryGetCrcExtra(messageId, out var crcExtra))
            // Unknown message id for current dialect/provider.
            // Frame boundary was valid, but we cannot validate CRC.
            // Skip frame and keep parsing subsequent buffered data.
            return true;

        var receivedCrcOffset = HeaderLength + payloadLength;

        var receivedCrc = (ushort)(rawBytes[receivedCrcOffset] | (rawBytes[receivedCrcOffset + 1] << 8));

        var calculatedCrc = MavLinkCrc.Calculate(rawBytes.AsSpan(1, HeaderLength - 1 + payloadLength), crcExtra);

        if (receivedCrc != calculatedCrc)
            // Invalid frame. Skip it and keep parsing subsequent data.
            return true;

        var payload = rawBytes.AsMemory(HeaderLength, payloadLength).ToArray();

        frame = new MavLinkFrame(
            systemId,
            componentId,
            ipEndPoint,
            messageId,
            sequence,
            payload,
            rawBytes,
            receivedAt);

        return true;
    }
}