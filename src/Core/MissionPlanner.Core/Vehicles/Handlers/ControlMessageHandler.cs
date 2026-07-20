using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers;

/// <summary>
/// Provides the public API for ControlMessageHandler.
/// </summary>
public sealed class ControlMessageHandler(
    IStatusTextHandler statusTextHandler,
    IParamValueVehicleHandler paramValueHandler,
    ICommandAckTracker commandAckTracker) : IVehicleMessageHandler
{
    /// <summary>
    /// Provides the public API for MessageTypes.
    /// </summary>
    public IReadOnlyCollection<Type> MessageTypes { get; } =
    [
        typeof(StatusTextMessage),
        typeof(ParamValueMessage),
        typeof(CommandAckMessage)
    ];

    /// <summary>
    /// Provides the public API for HandleAsync.
    /// </summary>
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
