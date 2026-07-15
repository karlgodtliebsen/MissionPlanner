using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler.Abstractions;

/// <summary>
/// Capability-oriented adapter from one or more MAVLink messages to domain state.
/// </summary>
public interface IVehicleMessageHandler
{
    IReadOnlyCollection<Type> MessageTypes { get; }

    ValueTask HandleAsync(MavLinkMessage message, CancellationToken cancellationToken);
}
