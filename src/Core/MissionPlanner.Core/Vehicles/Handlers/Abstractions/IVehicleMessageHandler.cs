using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers.Abstractions;

/// <summary>
/// Capability-oriented adapter from one or more MAVLink messages to domain state.
/// </summary>
public interface IVehicleMessageHandler
{
    /// <summary>
    /// Provides the public API for MessageTypes.
    /// </summary>
    IReadOnlyCollection<Type> MessageTypes { get; }

    /// <summary>
    /// Provides the public API for HandleAsync.
    /// </summary>
    ValueTask HandleAsync(MavLinkMessage message, CancellationToken cancellationToken);
}
