using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>
/// Provides the public API for IVehicleMessageDispatcher.
/// </summary>
public interface IVehicleMessageDispatcher
{
    /// <summary>
    /// Provides the public API for DispatchAsync.
    /// </summary>
    ValueTask<bool> DispatchAsync(MavLinkMessage message, CancellationToken cancellationToken);
}
