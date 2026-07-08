using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Services.Abstractions;
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
/// <param name="paramValueHandler">The handler for parameter value messages.</param>
/// <param name="commandAckTracker"></param>
/// <param name="eventHub"></param>
/// <param name="logger"></param>
public sealed class VehicleMessagePump(
    IHeartbeatVehicleHandler heartbeatHandler,
    IPositionVehicleHandler positionHandler,
    IAttitudeVehicleHandler attitudeHandler,
    IBatteryVehicleHandler batteryHandler,
    IStatusTextHandler statusTextHandler,
    IParamValueVehicleHandler paramValueHandler,
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
        /*Action<MavLinkMessage> message,*/
        //logger.LogTrace("VehicleMessagePump - Starting Event Subscription.");
        subscription = eventHub.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, HandleMessage);
        return Task.CompletedTask;
    }

    private async Task HandleMessage(MavLinkMessage message, CancellationToken cancellationToken)
    {
        //logger.LogTrace("VehicleMessagePump - Received {MessageType} {@Message}", message.GetType().Name, message);
        switch (message)
        {
            case HeartbeatMessage heartbeat:
                await heartbeatHandler.Handle(heartbeat, cancellationToken);
                await eventHub.PublishAsync<HeartbeatMessage>(MavLinkEventTopics.NewMessage, heartbeat, cancellationToken);
                break;
            case GlobalPositionIntMessage position:
                await positionHandler.Handle(position, cancellationToken);
                await eventHub.PublishAsync<GlobalPositionIntMessage>(MavLinkEventTopics.NewMessage, position, cancellationToken);
                break;

            case AttitudeMessage attitude:
                await attitudeHandler.Handle(attitude, cancellationToken);
                await eventHub.PublishAsync<AttitudeMessage>(MavLinkEventTopics.NewMessage, attitude, cancellationToken);
                break;

            case SysStatusMessage sysStatus:
                await batteryHandler.Handle(sysStatus, cancellationToken);
                await eventHub.PublishAsync<SysStatusMessage>(MavLinkEventTopics.NewMessage, sysStatus, cancellationToken);
                break;

            case StatusTextMessage statusText:
                await statusTextHandler.Handle(statusText, cancellationToken);
                await eventHub.PublishAsync<StatusTextMessage>(MavLinkEventTopics.NewMessage, statusText, cancellationToken);
                break;

            case ParamValueMessage paramValue:
                await paramValueHandler.Handle(paramValue, cancellationToken);
                await eventHub.PublishAsync<ParamValueMessage>(MavLinkEventTopics.NewMessage, paramValue, cancellationToken);
                break;

            case CommandAckMessage commandAck:
                commandAckTracker.Handle(commandAck);
                await eventHub.PublishAsync<CommandAckMessage>(MavLinkEventTopics.NewMessage, commandAck, cancellationToken);
                break;
            default:
                logger.LogError("VehicleMessagePump - Received unknown message type: {MessageType} {@Message}", message.GetType().Name, message);
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
