using Microsoft.Extensions.Logging;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.MavLink.Decoding.Utils;

/// <summary>
/// Decodes MAVLink messages using a collection of specific message decoders.
/// </summary>
public sealed class MavLinkMessageDecoderHandler : IMavLinkMessageDecodeHandler
{
    private readonly MavLinkMessageDecoders decoders;

    private readonly ILogger<MavLinkMessageDecoderHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MavLinkMessageDecoderHandler"/> class.
    /// </summary>
    /// <param name="decoders">The collection of specific message decoders.</param>
    /// <param name="logger">The logger instance.</param>
    public MavLinkMessageDecoderHandler(MavLinkMessageDecoders decoders, ILogger<MavLinkMessageDecoderHandler> logger)
    {
        this.decoders = decoders;
        this.logger = logger;
    }


    /// <inheritdoc />
    public bool TryDecode(MavLinkFrame frame, out MavLinkMessage? message)
    {
        logger.LogTrace("MavLinkMessageDecoderHandler - Decoding MAVLink frame: {SystemId}, {ComponentId}, {MessageId}", frame.SystemId, frame.ComponentId, frame.MessageId);
        if (decoders.TryDecode(frame, out message))
        {
            if (message is RawMavLinkMessage raw)
            {
                logger.LogDebug(
                    "Using raw MAVLink fallback. MessageId={MessageId}, MessageName={MessageName}, PayloadLength={PayloadLength}",
                    raw.MessageId,
                    raw.MessageName,
                    raw.Payload.Length);
            }
            else
            {
                logger.LogTrace("MavLinkMessageDecoderHandler - Decoded MAVLink frame MessageId={MessageId} as {MessageType}", frame.MessageId, message?.GetType().Name);
            }

            return true;
        }

        message = null;
        return false;
    }
}
