using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.VehicleHandler;
using MissionPlanner.Core.VehicleHandler.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.Core.Services;

/// <summary>
/// Pumps messages from a vehicle connection and handles them.
/// </summary>
/// <param name="heartbeatHandler">The handler for heartbeat messages.</param>
/// <param name="positionHandler">The handler for position messages.</param>
/// <param name="attitudeHandler">The handler for attitude messages.</param>
/// <param name="batteryHandler">The handler for battery status messages.</param>
/// <param name="statusTextHandler"></param>
/// <param name="commandAckTracker"></param>
/// <param name="eventHub"></param>
/// <param name="logger"></param>
public sealed class VehicleMessagePump(
    IHeartbeatVehicleHandler heartbeatHandler,
    IPositionVehicleHandler positionHandler,
    IAttitudeVehicleHandler attitudeHandler,
    IBatteryVehicleHandler batteryHandler,
    IStatusTextHandler statusTextHandler,
    ICommandAckTracker commandAckTracker,
    IEventHub eventHub,
    ILogger<VehicleMessagePump> logger)
    : IVehicleMessagePump
{
    private IDisposable? subscription;

    /// <summary>
    /// Starts pumping messages from the vehicle connection.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogTrace("VehicleMessagePump - Starting Event Subscription.");
        subscription = eventHub.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, HandleMessage);
        return Task.CompletedTask;
    }

    private async Task HandleMessage(MavLinkMessage message, CancellationToken cancellationToken)
    {
        logger.LogTrace("VehicleMessagePump - Received {MessageType}", message.GetType().Name);
        switch (message)
        {
            case HeartbeatMessage heartbeat:
                heartbeatHandler.Handle(heartbeat);
                await eventHub.PublishAsync<HeartbeatMessage>(MavLinkEventTopics.ReceivedMessage, heartbeat, cancellationToken);
                break;

            case GlobalPositionIntMessage position:
                positionHandler.Handle(position);
                await eventHub.PublishAsync<GlobalPositionIntMessage>(MavLinkEventTopics.ReceivedMessage, position, cancellationToken);
                break;

            case AttitudeMessage attitude:
                attitudeHandler.Handle(attitude);
                await eventHub.PublishAsync<AttitudeMessage>(MavLinkEventTopics.ReceivedMessage, attitude, cancellationToken);
                break;

            case SysStatusMessage sysStatus:
                batteryHandler.Handle(sysStatus);
                await eventHub.PublishAsync<SysStatusMessage>(MavLinkEventTopics.ReceivedMessage, sysStatus, cancellationToken);
                break;

            case StatusTextMessage statusText:
                statusTextHandler.Handle(statusText);
                await eventHub.PublishAsync<StatusTextMessage>(MavLinkEventTopics.ReceivedMessage, statusText, cancellationToken);
                break;

            case CommandAckMessage commandAck:
                commandAckTracker.Handle(commandAck);
                await eventHub.PublishAsync<CommandAckMessage>(MavLinkEventTopics.ReceivedMessage, commandAck, cancellationToken);
                break;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        subscription?.Dispose();
        subscription = null;
        return ValueTask.CompletedTask;
    }
}
