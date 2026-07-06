using System.Net;
using Microsoft.Extensions.Logging;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.Transport;

namespace MissionPlanner.MavLink.Services;

/// <summary>
/// Represents a connection to a MAVLink device, managing the reception and decoding of MAVLink frames and messages.
/// </summary>
public sealed class MavLinkConnection : IMavLinkConnection
{
    private readonly IMavLinkClient client;
    private readonly IMavLinkFrameParser frameParser;
    private readonly IMavLinkMessageDecoder messageDecoder;
    private readonly IEventHub eventHub;
    private readonly ILogger<MavLinkConnection> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MavLinkConnection"/> class with the specified dependencies. 
    /// </summary>
    /// <param name="client"></param>
    /// <param name="frameParser"></param>
    /// <param name="messageDecoder"></param>
    /// <param name="eventHub"></param>
    /// <param name="logger">The logger instance to use for logging.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public MavLinkConnection(IMavLinkClient client, IMavLinkFrameParser frameParser, IMavLinkMessageDecoder messageDecoder, IEventHub eventHub, ILogger<MavLinkConnection> logger)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.frameParser = frameParser ?? throw new ArgumentNullException(nameof(frameParser));
        this.messageDecoder = messageDecoder ?? throw new ArgumentNullException(nameof(messageDecoder));
        this.eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        this.client.DataReceived += OnDataReceivedAsync;
    }

    /// <summary>
    /// Starts the MAVLink connection, allowing it to receive and process incoming data.
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await client.StartAsync(cancellationToken).ConfigureAwait(false);
        logger.LogTrace("MavLinkConnection - MAVLink connection started.");
    }


    /// <summary>
    /// Sends raw MAVLink data through the connection.
    /// </summary>
    /// <param name="data">The raw MAVLink data to send.</param>
    /// <param name="ipEndpoint"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    public async ValueTask SendRawAsync(ReadOnlyMemory<byte> data, IPEndPoint ipEndpoint, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("MavLinkConnection - Sending raw MAVLink data.");
        await client.SendAsync(data, ipEndpoint, cancellationToken).ConfigureAwait(false);
    }

    private async Task OnDataReceivedAsync(MavLinkDataReceived received, CancellationToken cancellationToken)
    {
        logger.LogTrace("MavLinkConnection - Data received at {ReceivedAt}", received.ReceivedAt);
        var parsedFrames = frameParser.Parse(received.Data.Span, received.RemoteEndpoint, received.ReceivedAt);

        foreach (var frame in parsedFrames)
        {
            logger.LogTrace("MavLinkConnection - Processing frame {frame}", frame.MessageId);

            await eventHub.PublishAsync(MavLinkEventTopics.ReceivedFrame, frame, cancellationToken);

            if (messageDecoder.TryDecode(frame, out var message) && message is not null)
            {
                logger.LogTrace("MavLinkConnection - Writing Decoded Message { MessageType}", message.GetType().Name);

                //IPEndPoint ipEndpoint,

                await eventHub.PublishAsync(MavLinkEventTopics.ReceivedMessage, message, cancellationToken);
                return;
            }
            else
            {
                logger.LogError("MavLinkConnection - Failed to decode message from frame {frame}", frame.MessageId);
            }
        }
    }


    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        client.DataReceived -= OnDataReceivedAsync;
        await client.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
        logger.LogTrace("MavLinkConnection - MAVLink connection disposed.");
    }
}
