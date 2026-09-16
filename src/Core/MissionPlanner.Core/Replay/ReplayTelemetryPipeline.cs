using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.EventHub.Events;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Replay;

/// <summary>Runs replay frames through the normal decoder and cohesive telemetry handlers using an isolated registry.</summary>
public sealed class ReplayTelemetryPipeline : IReplayTelemetryPipeline
{
    private static readonly TransportEndPoint replayEndpoint = new("Replay", "read-only-tlog");
    private readonly IMavLinkFrameParser frameParser;
    private readonly IMavLinkMessageDecodeHandler messageDecoder;
    private readonly IDateTimeProvider clock;
    private readonly ILoggerFactory loggerFactory;
    private readonly IDomainEventHub replayEventHub = new ReplayDomainEventHub();
    private IVehicleRegistry registry;
    private IVehicleMessageDispatcher dispatcher;
    private StatusTextHandler statusText;
    private VehicleMessageStore messageStore;

    /// <summary>Initializes the isolated replay telemetry pipeline.</summary>
    /// <param name="frameParser">A parser instance dedicated to replay frames.</param>
    /// <param name="messageDecoder">The generated MAVLink message decoder catalog.</param>
    /// <param name="clock">Application clock used by replay-only vehicle sessions.</param>
    /// <param name="loggerFactory">Logger factory for isolated domain services.</param>
    public ReplayTelemetryPipeline(
        IMavLinkFrameParser frameParser,
        IMavLinkMessageDecodeHandler messageDecoder,
        IDateTimeProvider clock,
        ILoggerFactory loggerFactory)
    {
        this.frameParser = frameParser;
        this.messageDecoder = messageDecoder;
        this.clock = clock;
        this.loggerFactory = loggerFactory;
        (registry, dispatcher, statusText, messageStore) = CreatePipeline();
    }

    /// <inheritdoc />
    public IReadOnlyList<VehicleState> Vehicles => registry.Vehicles.Select(vehicle => vehicle.State).ToArray();

    /// <summary>Gets bounded assembled status text in replay arrival order, isolated from live history.</summary>
    public IReadOnlyList<VehicleStatusText> StatusMessages => registry.Vehicles
        .SelectMany(vehicle => messageStore.GetMessages(vehicle.Id)).OrderBy(message => message.Identity).ToArray();

    /// <inheritdoc />
    public void Reset()
    {
        frameParser.Reset();
        (registry, dispatcher, statusText, messageStore) = CreatePipeline();
    }

    /// <inheritdoc />
    public async ValueTask<bool> ProcessAsync(
        ReadOnlyMemory<byte> packet,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default)
    {
        frameParser.Reset();
        var frames = frameParser.Parse(packet.Span, replayEndpoint, receivedAt);
        if (frames.Count != 1 || !messageDecoder.TryDecode(frames[0], out var message) || message is null)
        {
            return false;
        }

        await statusText.FlushExpiredAsync(receivedAt, cancellationToken).ConfigureAwait(false);
        if (message is StatusTextMessage text)
        {
            await statusText.Handle(text, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await dispatcher.DispatchAsync(message, cancellationToken).ConfigureAwait(false);
        }
        return true;
    }

    private (IVehicleRegistry Registry, IVehicleMessageDispatcher Dispatcher, StatusTextHandler StatusText, VehicleMessageStore Messages) CreatePipeline()
    {
        IVehicleRegistry replayRegistry = new VehicleRegistry(
            replayEventHub,
            clock,
            loggerFactory.CreateLogger<VehicleRegistry>());
        IVehicleMessageHandler[] handlers =
        [
            new FlightTelemetryHandler(replayRegistry, replayEventHub),
            new NavigationTelemetryHandler(replayRegistry, replayEventHub),
            new PowerTelemetryHandler(replayRegistry, replayEventHub),
            new RadioTelemetryHandler(replayRegistry, replayEventHub),
            new HealthTelemetryHandler(replayRegistry, replayEventHub),
            new SensorTelemetryHandler(replayRegistry, replayEventHub)
        ];
        var options = Options.Create(new VehicleMessageStoreOptions());
        var messages = new VehicleMessageStore(options);
        var textHandler = new StatusTextHandler(replayRegistry, messages, replayEventHub, options,
            loggerFactory.CreateLogger<StatusTextHandler>(), useRecordedTime: true);
        return (replayRegistry, new VehicleMessageDispatcher(handlers), textHandler, messages);
    }

    private sealed class ReplayDomainEventHub : IDomainEventHub
    {
        public IDisposable SubscribeDomainEventAsync<T>(Func<T, CancellationToken, Task> handler)
            where T : IDomainEvent => EmptySubscription.Instance;

        public Task PublishDomainEventAsync<T>(T data, CancellationToken cancellationToken = default)
            where T : IDomainEvent => Task.CompletedTask;
    }

    private sealed class EmptySubscription : IDisposable
    {
        public static EmptySubscription Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
