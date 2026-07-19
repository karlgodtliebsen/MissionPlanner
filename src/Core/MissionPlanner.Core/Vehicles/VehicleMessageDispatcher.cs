using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Dispatches MAVLink messages to the appropriate vehicle message handlers.
/// </summary>
public sealed class VehicleMessageDispatcher : IVehicleMessageDispatcher
{
    private readonly IReadOnlyDictionary<Type, IVehicleMessageHandler> handlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="VehicleMessageDispatcher"/> class with the specified vehicle message handlers.
    /// </summary>
    /// <param name="handlers"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public VehicleMessageDispatcher(IEnumerable<IVehicleMessageHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var map = new Dictionary<Type, IVehicleMessageHandler>();
        foreach (var handler in handlers)
        {
            foreach (var messageType in handler.MessageTypes)
            {
                if (!typeof(MavLinkMessage).IsAssignableFrom(messageType))
                {
                    throw new InvalidOperationException($"{messageType.FullName} is not a MAVLink message type.");
                }

                if (!map.TryAdd(messageType, handler))
                {
                    throw new InvalidOperationException(
                        $"More than one vehicle message handler is registered for {messageType.FullName}.");
                }
            }
        }

        this.handlers = map;
    }

    /// <summary>
    /// Dispatches the specified MAVLink message to the appropriate handler.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async ValueTask<bool> DispatchAsync(MavLinkMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!handlers.TryGetValue(message.GetType(), out var handler))
        {
            return false;
        }

        await handler.HandleAsync(message, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
