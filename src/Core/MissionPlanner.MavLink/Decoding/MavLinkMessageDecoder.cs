using Microsoft.Extensions.Logging;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Decodes MAVLink messages using a collection of specific message decoders.
/// </summary>
public sealed class MavLinkMessageDecoder : IMavLinkMessageDecoder
{
    private readonly ILogger<MavLinkMessageDecoder> logger;
    private readonly IReadOnlyList<IMavLinkMessageDecoder> decoders;

    /// <summary>
    /// Initializes a new instance of the <see cref="MavLinkMessageDecoder"/> class.
    /// </summary>
    /// <param name="options">The options containing the list of message decoders.</param>
    /// <param name="logger">The logger instance.</param>
    public MavLinkMessageDecoder(MavLinkMessageDecoders options, ILogger<MavLinkMessageDecoder> logger)
    {
        this.logger = logger;
        decoders = options.Decoders;
    }


    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        logger.LogTrace("MavLinkMessageDecoder - Decoding MAVLink frame: {SystemId}, {ComponentId}, {MessageId}", frame.SystemId, frame.ComponentId, frame.MessageId);
        foreach (var decoder in decoders)
            if (decoder.TryDecode(frame, out message))
            {
                logger.LogInformation("MavLinkMessageDecoder - Decoded MAVLink frame MessageId={MessageId} as {MessageType}", frame.MessageId, message?.GetType().Name);
                return true;
            }

        logger.LogWarning(
            "MavLinkMessageDecoder - No decoder accepted MAVLink frame MessageId={MessageId}, SystemId={SystemId}, ComponentId={ComponentId}, PayloadLength={PayloadLength}",
            frame.MessageId,
            frame.SystemId,
            frame.ComponentId,
            frame.Payload.Length);


        message = null;
        return false;
    }
}