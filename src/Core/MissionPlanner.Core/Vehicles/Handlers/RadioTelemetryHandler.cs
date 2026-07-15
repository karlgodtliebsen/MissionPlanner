using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Core.Vehicles.Observations;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers;

public sealed class RadioTelemetryHandler(
    IVehicleRegistry vehicleRegistry,
    IDomainEventHub domainEventHub)
    : VehicleTelemetryHandlerBase(vehicleRegistry, domainEventHub), IVehicleMessageHandler
{
    public IReadOnlyCollection<Type> MessageTypes { get; } = [typeof(RcChannelsMessage)];

    public async ValueTask HandleAsync(MavLinkMessage message, CancellationToken cancellationToken)
    {
        if (message is not RcChannelsMessage channels)
        {
            throw new ArgumentException("Unsupported message type.", nameof(message));
        }

        var vehicle = GetVehicle(channels);
        if (vehicle is null)
        {
            return;
        }

        vehicle.ApplyRadio(new VehicleRadioObservation(
            channels.ChannelCount,
            channels.ChannelsRaw,
            channels.Rssi == byte.MaxValue
                ? null
                : (int)Math.Round(channels.Rssi * 100.0 / 254.0),
            channels.ReceivedAt));

        await PublishStateAsync(vehicle, cancellationToken).ConfigureAwait(false);
    }
}
