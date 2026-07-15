using Microsoft.Extensions.Logging;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Library.EventHub;

/// <summary>
/// Represents a hub for publishing and subscribing to domain events.
/// </summary>
/// <param name="logger">The logger instance.</param>
public class DomainEventHub(ILogger<EventHub> logger) : EventHub(logger), IDomainEventHub
{
    /// <inheritdoc/>
    public virtual IDisposable SubscribeDomainEventAsync<T>(Func<T, CancellationToken, Task> handler) where T : IDomainEvent
    {
        var eventName = typeof(T).FullName!;
        return SubscribeAsync<T>(eventName, handler);
    }

    /// <inheritdoc/>
    public virtual async Task PublishDomainEventAsync<T>(T data, CancellationToken cancellationToken = default) where T : IDomainEvent
    {
        var eventName = data.Name;
        var key = KeyGenerator.GetEventKey<T>(eventName);

        await PublishAsync<IDomainEvent>(key, eventName, data, cancellationToken);
        eventName = data.GetType().FullName!;
        await PublishAsync(eventName, data, cancellationToken);
    }
}
