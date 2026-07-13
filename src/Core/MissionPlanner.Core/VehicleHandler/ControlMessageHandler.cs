using MissionPlanner.Core.Commands;
using MissionPlanner.Core.VehicleHandler.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.Core.VehicleHandler;

public sealed class ControlMessageHandler(
    IStatusTextHandler statusTextHandler,
    IParamValueVehicleHandler paramValueHandler,
    ICommandAckTracker commandAckTracker) : IVehicleMessageHandler
{
    public IReadOnlyCollection<Type> MessageTypes { get; } =
    [
        typeof(StatusTextMessage),
        typeof(ParamValueMessage),
        typeof(CommandAckMessage)
    ];

    public async ValueTask HandleAsync(MavLinkMessage message, CancellationToken cancellationToken)
    {
        switch (message)
        {
            case StatusTextMessage statusText:
                await statusTextHandler.Handle(statusText, cancellationToken).ConfigureAwait(false);
                break;
            case ParamValueMessage parameter:
                await paramValueHandler.Handle(parameter, cancellationToken).ConfigureAwait(false);
                break;
            case CommandAckMessage acknowledgement:
                commandAckTracker.Handle(acknowledgement);
                break;
            default:
                throw new ArgumentException("Unsupported message type.", nameof(message));
        }
    }
}
