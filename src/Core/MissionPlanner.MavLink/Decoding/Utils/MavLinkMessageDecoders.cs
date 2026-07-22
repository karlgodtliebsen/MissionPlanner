using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding.Utils;

/// <summary>
/// Represents a collection of MAVLink message decoders.
/// </summary>
public sealed class MavLinkMessageDecoders
{
    private readonly IReadOnlyDictionary<uint, IMavLinkMessageDecoder> decoders;
    private readonly RawMavLinkMessageDecoder rawDecoder;

    /// <summary>
    /// Initializes a new instance of the <see cref="MavLinkMessageDecoders"/> class with the specified decoders.   
    /// </summary>
    /// <param name="decoders">The typed message decoders.</param>
    /// <param name="definitions">The selected-dialect message-definition registry.</param>
    public MavLinkMessageDecoders(
        IEnumerable<IMavLinkMessageDecoder> decoders,
        IMavLinkMessageDefinitionRegistry definitions)
    {
        this.decoders = decoders.ToDictionary(x => x.MessageId);
        rawDecoder = new RawMavLinkMessageDecoder(definitions);
    }

    /// <summary>
    /// Tries to decode a MAVLink message from the given frame.
    /// </summary>
    /// <param name="frame">The MAVLink frame containing the message.</param>
    /// <param name="message">The decoded MAVLink message, if successful.</param>
    /// <returns>True if the message was successfully decoded; otherwise, false.</returns>
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        if (decoders.TryGetValue(frame.MessageId, out var decoder))
        {
            if (decoder.TryDecode(frame, out message) && message is not null)
            {
                return true;
            }
        }

        message = rawDecoder.Decode(frame);
        return true;
    }
}
