using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.FlightData.Components;

/// <summary>Routes peripheral messages into the component registry without polluting VehicleState.</summary>
public sealed class PeripheralComponentHandler(VehicleComponentRegistry registry) : IVehicleMessageHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<Type> MessageTypes { get; } = [typeof(UavionixAdsbOutStatusMessage), typeof(AdsbVehicleMessage)];

    /// <inheritdoc />
    public ValueTask HandleAsync(MavLinkMessage message, CancellationToken cancellationToken)
    {
        switch (message)
        {
            case UavionixAdsbOutStatusMessage status:
                registry.Observe(status);
                break;
            case AdsbVehicleMessage track:
                registry.Observe(track);
                break;
            default:
                throw new ArgumentException("Unsupported peripheral message type.", nameof(message));
        }

        return ValueTask.CompletedTask;
    }
}
