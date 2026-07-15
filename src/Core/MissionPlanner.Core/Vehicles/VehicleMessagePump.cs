using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// A message pump that listens for MAVLink messages and dispatches them to the appropriate handlers.
/// </summary>
/// <param name="dispatcher">The dispatcher responsible for handling MAVLink messages.</param>
/// <param name="eventHub">The event hub used to subscribe to MAVLink message events.</param>
/// <param name="logger">The logger used for logging message pump activities.</param>
public sealed class VehicleMessagePump(IVehicleMessageDispatcher dispatcher, IEventHub eventHub, ILogger<VehicleMessagePump> logger) : IVehicleMessagePump
{
    private IDisposable? subscription;

    /// <summary>
    /// Starts the message pump, subscribing to MAVLink message events.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous start operation.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        subscription ??= eventHub.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, HandleMessageAsync);
        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(MavLinkMessage message, CancellationToken cancellationToken)
    {
        if (await dispatcher.DispatchAsync(message, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        // Valid but deliberately domain-ignored messages should remain Debug/Trace, not Error.
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("No domain handler registered for MAVLink message {MessageType} ({MessageId}).", message.GetType().Name, message.MessageId);
        }
    }

    /// <summary>
    /// Disposes the message pump, unsubscribing from MAVLink message events.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    public ValueTask DisposeAsync()
    {
        subscription?.Dispose();
        subscription = null;
        return ValueTask.CompletedTask;
    }
}
