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
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("VehicleMessagePump - Starting Event Subscription.");
        }

        subscription = eventHub.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, HandleMessage);
        return Task.CompletedTask;
    }

    private async Task HandleMessage(MavLinkMessage message, CancellationToken cancellationToken)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("VehicleMessagePump - Received {MessageType} {@Message}", message.GetType().Name, message);
        }

        switch (message)
        {
            case Ahrs2Message ahrs2:
                //await ahrs2Handler.Handle(ahrs2, cancellationToken);
                await eventHub.PublishAsync<Ahrs2Message>(MavLinkEventTopics.NewMessage, ahrs2, cancellationToken);
                break;
            case AttitudeMessage attitude:
                await attitudeHandler.Handle(attitude, cancellationToken);
                await eventHub.PublishAsync<AttitudeMessage>(MavLinkEventTopics.NewMessage, attitude, cancellationToken);
                break;

            case BatteryStatusMessage batteryStatus:
                //await batteryHandler.Handle(batteryStatus, cancellationToken);
                await eventHub.PublishAsync<BatteryStatusMessage>(MavLinkEventTopics.NewMessage, batteryStatus, cancellationToken);
                break;

            case CommandAckMessage commandAck:
                commandAckTracker.Handle(commandAck);
                await eventHub.PublishAsync<CommandAckMessage>(MavLinkEventTopics.NewMessage, commandAck, cancellationToken);
                break;

            case EkfStatusReportMessage ekfStatusReport:
                //await ekfStatusReportHandler.Handle(ekfStatusReport, cancellationToken);
                await eventHub.PublishAsync<EkfStatusReportMessage>(MavLinkEventTopics.NewMessage, ekfStatusReport, cancellationToken);
                break;
            case GlobalPositionIntMessage position:
                await positionHandler.Handle(position, cancellationToken);
                await eventHub.PublishAsync<GlobalPositionIntMessage>(MavLinkEventTopics.NewMessage, position, cancellationToken);
                break;

            case GpsRawIntMessage gpsRawInt:
                //await gpsRawIntHandler.Handle(gpsRawInt, cancellationToken);
                await eventHub.PublishAsync<GpsRawIntMessage>(MavLinkEventTopics.NewMessage, gpsRawInt, cancellationToken);
                break;

            case HeartbeatMessage heartbeat:
                await heartbeatHandler.Handle(heartbeat, cancellationToken);
                await eventHub.PublishAsync<HeartbeatMessage>(MavLinkEventTopics.NewMessage, heartbeat, cancellationToken);
                break;
            case LocalPositionNedMessage localPositionNed:
                //await localPositionNedHandler.Handle(localPositionNed, cancellationToken);
                await eventHub.PublishAsync<LocalPositionNedMessage>(MavLinkEventTopics.NewMessage, localPositionNed, cancellationToken);
                break;
            case MemInfoMessage memoryVect:
                //await memoryHandler.Handle(memoryVect, cancellationToken);
                await eventHub.PublishAsync<MemInfoMessage>(MavLinkEventTopics.NewMessage, memoryVect, cancellationToken);
                break;

            case MemoryVectMessage memoryVect:
                //await memoryVectHandler.Handle(memoryVect, cancellationToken);
                await eventHub.PublishAsync<MemoryVectMessage>(MavLinkEventTopics.NewMessage, memoryVect, cancellationToken);
                break;

            case MissionCurrentMessage commandAck:
                // missionCurrentHandler.Handle(commandAck);
                await eventHub.PublishAsync<MissionCurrentMessage>(MavLinkEventTopics.NewMessage, commandAck, cancellationToken);
                break;
            case NavControllerOutputMessage navControllerOutput:
                //   navControllerOutputHandler.Handle(navControllerOutput);
                await eventHub.PublishAsync<NavControllerOutputMessage>(MavLinkEventTopics.NewMessage, navControllerOutput, cancellationToken);
                break;
            case ParamValueMessage paramValue:
                await paramValueHandler.Handle(paramValue, cancellationToken);
                await eventHub.PublishAsync<ParamValueMessage>(MavLinkEventTopics.NewMessage, paramValue, cancellationToken);
                break;

            case PowerStatusMessage powerStatus:
                //  powerStatusHandler.Handle(powerStatus);
                await eventHub.PublishAsync<PowerStatusMessage>(MavLinkEventTopics.NewMessage, powerStatus, cancellationToken);
                break;

            case RawImuMessage rawImu:
                //    rawImuHandler.Handle(rawImu);
                await eventHub.PublishAsync<RawImuMessage>(MavLinkEventTopics.NewMessage, rawImu, cancellationToken);
                break;

            case RawMavLinkMessage rawMavLink:
                //    rawMavLinkHandler.Handle(rawMavLink);
                await eventHub.PublishAsync<RawMavLinkMessage>(MavLinkEventTopics.NewMessage, rawMavLink, cancellationToken);
                break;
            case RcChannelsMessage rcChannels:
                //    rcChannelsHandler.Handle(rcChannels);
                await eventHub.PublishAsync<RcChannelsMessage>(MavLinkEventTopics.NewMessage, rcChannels, cancellationToken);
                break;
            case ScaledPressureMessage scaledPressure:
                //   scaledPressureHandler.Handle(scaledPressure);
                await eventHub.PublishAsync<ScaledPressureMessage>(MavLinkEventTopics.NewMessage, scaledPressure, cancellationToken);
                break;
            case ServoOutputRawMessage servoOutputRaw:
                //await batteryHandler.Handle(servoOutputRaw, cancellationToken);
                await eventHub.PublishAsync<ServoOutputRawMessage>(MavLinkEventTopics.NewMessage, servoOutputRaw, cancellationToken);
                break;
            case StatusTextMessage statusText:
                await statusTextHandler.Handle(statusText, cancellationToken);
                await eventHub.PublishAsync<StatusTextMessage>(MavLinkEventTopics.NewMessage, statusText, cancellationToken);
                break;

            case SysStatusMessage sysStatus:
                await batteryHandler.Handle(sysStatus, cancellationToken);
                await eventHub.PublishAsync<SysStatusMessage>(MavLinkEventTopics.NewMessage, sysStatus, cancellationToken);
                break;


            case TimeSyncMessage timeSync:
                //   timeSyncHandler.Handle(timeSync);
                await eventHub.PublishAsync<TimeSyncMessage>(MavLinkEventTopics.NewMessage, timeSync, cancellationToken);
                break;

            case VfrHudMessage vfrHud:
                //   vfrHudHandler.Handle(vfrHud);
                await eventHub.PublishAsync<VfrHudMessage>(MavLinkEventTopics.NewMessage, vfrHud, cancellationToken);
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
