using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding;

internal abstract class GeneratedMavLinkMessageDecoder : IMavLinkMessageDecoder
{
    private readonly MavLinkMessageDefinition definition;

    protected GeneratedMavLinkMessageDecoder(IMavLinkMessageDefinitionRegistry definitions, uint messageId)
    {
        if (!definitions.TryGet(messageId, out var registeredDefinition))
        {
            throw new ArgumentException($"MAVLink message ID {messageId} is not registered.", nameof(messageId));
        }

        definition = registeredDefinition;
    }

    public uint MessageId => definition.MessageId;

    public byte CrcExtra => definition.CrcExtra;

    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        message = null;
        var minimumWirePayloadLength = definition.MaximumPayloadLength == 0 ? 0 : 1;
        if (frame.MessageId != MessageId ||
            frame.Payload.Length < minimumWirePayloadLength ||
            frame.Payload.Length > definition.MaximumPayloadLength)
        {
            return false;
        }

        Span<byte> paddedPayload = stackalloc byte[byte.MaxValue];
        paddedPayload.Clear();
        frame.Payload.Span.CopyTo(paddedPayload);
        message = DecodeCore(frame, paddedPayload[..definition.MaximumPayloadLength]);
        return true;
    }

    protected abstract MavLinkMessage DecodeCore(MavLinkFrame frame, ReadOnlySpan<byte> payload);
}
