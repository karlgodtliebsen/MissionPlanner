using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Represents a collection of MAVLink message decoders.
/// </summary>
public sealed class MavLinkMessageDecoders
{
    /// <summary>
    /// Gets the list of MAVLink message decoders.
    /// </summary>  
    public IReadOnlyList<IMavLinkMessageDecoder> Decoders { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MavLinkMessageDecoder"/> class.
    /// </summary>
    /// <param name="options">The options containing the list of message decoders.</param>
    public MavLinkMessageDecoders(IList<IMavLinkMessageDecoder> options)
    {
        Decoders = options.ToArray();
    }
}