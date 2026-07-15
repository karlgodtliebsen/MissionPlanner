using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.VehicleHandler.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Services;

public sealed class VehicleMessageDispatcher : IVehicleMessageDispatcher
{
    private readonly IReadOnlyDictionary<Type, IVehicleMessageHandler> handlers;

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
