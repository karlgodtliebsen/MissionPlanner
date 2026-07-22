using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Core.Vehicles.Observations;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers;

/// <summary>
/// Provides the public API for RadioTelemetryHandler.
/// </summary>
public sealed class RadioTelemetryHandler(
    IVehicleRegistry vehicleRegistry,
    IDomainEventHub domainEventHub)
    : VehicleTelemetryHandlerBase(vehicleRegistry, domainEventHub), IVehicleMessageHandler
{
    /// <summary>
    /// Provides the public API for MessageTypes.
    /// </summary>
    public IReadOnlyCollection<Type> MessageTypes { get; } =
    [
        typeof(RcChannelsMessage),
        typeof(RadioStatusMessage),
        typeof(RadioMessage),
        typeof(ServoOutputRawMessage)
    ];

    /// <summary>
    /// Provides the public API for HandleAsync.
    /// </summary>
    public async ValueTask HandleAsync(MavLinkMessage message, CancellationToken cancellationToken)
    {
        var vehicle = GetVehicle(message);
        if (vehicle is null)
        {
            return;
        }

        var previous = vehicle.State;
        switch (message)
        {
            case RcChannelsMessage channels:
                vehicle.ApplyRadio(new VehicleRadioObservation(
                    channels.ChannelCount,
                    channels.ChannelsRaw,
                    channels.Rssi == byte.MaxValue ? null : (int)Math.Round(channels.Rssi * 100.0 / 254.0),
                    channels.ReceivedAt));
                break;
            case RadioStatusMessage status:
                ApplyLink(vehicle, status.Rssi, status.Remrssi, status.Txbuf, status.Noise, status.Remnoise, status.Rxerrors, status.Fixed, status.ReceivedAt);
                break;
            case RadioMessage radio:
                ApplyLink(vehicle, radio.Rssi, radio.Remrssi, radio.Txbuf, radio.Noise, radio.Remnoise, radio.Rxerrors, radio.Fixed, radio.ReceivedAt);
                break;
            case ServoOutputRawMessage servo:
                vehicle.ApplyServoOutputs(new VehicleServoOutputObservation(servo.Port, servo.ServoRaw, servo.ReceivedAt));
                break;
            default:
                throw new ArgumentException("Unsupported message type.", nameof(message));
        }

        await PublishStateIfChangedAsync(previous, vehicle, cancellationToken).ConfigureAwait(false);
    }

    private static void ApplyLink(VehicleSession vehicle, byte rssi, byte remoteRssi, byte transmitBuffer, byte localNoise, byte remoteNoise, ushort receiveErrors, ushort correctedPackets, DateTimeOffset observedAt)
    {
        vehicle.ApplyRadioLink(new VehicleRadioLinkObservation(
            rssi == byte.MaxValue ? null : rssi,
            remoteRssi == byte.MaxValue ? null : remoteRssi,
            transmitBuffer == byte.MaxValue ? null : transmitBuffer,
            localNoise == byte.MaxValue ? null : localNoise,
            remoteNoise == byte.MaxValue ? null : remoteNoise,
            receiveErrors,
            correctedPackets,
            observedAt));
    }
}
