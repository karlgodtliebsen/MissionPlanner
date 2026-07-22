using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Creates lossless raw messages for frames without a registered typed decoder.
/// </summary>
public sealed class RawMavLinkMessageDecoder
{
    private const byte MavLinkV1Magic = 0xfe;
    private const byte MavLinkV2Magic = 0xfd;
    private const byte SignedFlag = 0x01;
    private const int SignatureLength = 13;
    private readonly IMavLinkMessageDefinitionRegistry definitions;

    /// <summary>
    /// Initializes a raw-message decoder.
    /// </summary>
    /// <param name="definitions">The selected-dialect message-definition registry.</param>
    public RawMavLinkMessageDecoder(IMavLinkMessageDefinitionRegistry definitions)
    {
        this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
    }

    /// <summary>
    /// Creates a raw message while preserving the complete frame envelope.
    /// </summary>
    /// <param name="frame">The CRC-valid parsed frame.</param>
    /// <returns>The lossless raw message.</returns>
    public RawMavLinkMessage Decode(MavLinkFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var rawFrame = frame.RawBytes.ToArray();
        var version = rawFrame.Length == 0 ? (byte)0 : rawFrame[0] switch
        {
            MavLinkV1Magic => (byte)1,
            MavLinkV2Magic => (byte)2,
            _ => (byte)0
        };
        var incompatibilityFlags = version == 2 && rawFrame.Length > 2 ? rawFrame[2] : (byte)0;
        var compatibilityFlags = version == 2 && rawFrame.Length > 3 ? rawFrame[3] : (byte)0;
        var signature = version == 2
            && (incompatibilityFlags & SignedFlag) != 0
            && rawFrame.Length >= SignatureLength
                ? rawFrame[^SignatureLength..]
                : [];
        definitions.TryGet(frame.MessageId, out var definition);

        return new RawMavLinkMessage(
            version,
            frame.SystemId,
            frame.ComponentId,
            frame.EndPoint,
            frame.MessageId,
            frame.Sequence,
            incompatibilityFlags,
            compatibilityFlags,
            frame.Payload.ToArray(),
            signature,
            rawFrame,
            definition,
            frame.ReceivedAt);
    }
}
