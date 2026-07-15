using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Abstractions;

public interface IVehicleMessageDispatcher
{
    ValueTask<bool> DispatchAsync(MavLinkMessage message, CancellationToken cancellationToken);
}
