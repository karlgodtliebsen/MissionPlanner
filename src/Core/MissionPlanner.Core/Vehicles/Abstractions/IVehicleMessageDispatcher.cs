using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Services.Abstractions;

public interface IVehicleMessageDispatcher
{
    ValueTask<bool> DispatchAsync(MavLinkMessage message, CancellationToken cancellationToken);
}
