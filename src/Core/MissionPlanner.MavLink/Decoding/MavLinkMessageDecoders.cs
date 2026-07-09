using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

///// <summary>
///// Represents a collection of MAVLink message decoders.
///// </summary>
//public sealed class MavLinkMessageDecoders
//{
//    /// <summary>
//    /// Gets the list of MAVLink message decoders.
//    /// </summary>  
//    public IReadOnlyList<IMavLinkMessageDecoder> Decoders { get; }

//    /// <summary>
//    /// Initializes a new instance of the <see cref="MavLinkMessageDecoder"/> class.
//    /// </summary>
//    /// <param name="options">The options containing the list of message decoders.</param>
//    public MavLinkMessageDecoders(IList<IMavLinkMessageDecoder> options)
//    {
//        Decoders = options.ToArray();
//    }
//}

/// <summary>
/// Represents a collection of MAVLink message decoders.
/// </summary>
public sealed class MavLinkMessageDecoders
{
    private readonly IReadOnlyDictionary<uint, IMavLinkMessageDecoder> decoders;

    /// <summary>
    /// Initializes a new instance of the <see cref="MavLinkMessageDecoders"/> class with the specified decoders.   
    /// </summary>
    /// <param name="decoders"></param>
    public MavLinkMessageDecoders(IEnumerable<IMavLinkMessageDecoder> decoders)
    {
        this.decoders = decoders.ToDictionary(x => x.MessageId);
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
            return decoder.TryDecode(frame, out message);
        }

        message = null;
        return false;
    }
}
