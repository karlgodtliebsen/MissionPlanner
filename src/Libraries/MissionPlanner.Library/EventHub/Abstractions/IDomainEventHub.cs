using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Library.EventHub.Abstractions;

/// <summary>
/// Defines the contract for an event hub that allows subscribing to and publishing events. 
/// </summary>
public interface IDomainEventHub
{
    /// <summary>
    /// Subscribes For the DomainEvent specified Func signature
    /// </summary>
    /// <param name="handler"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    IDisposable SubscribeDomainEventAsync<T>(Func<T, CancellationToken, Task> handler) where T : IDomainEvent;

    /// <summary>
    /// Publish DomainEvent Async
    /// </summary>
    /// <param name="data"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task PublishDomainEventAsync<T>(T data, CancellationToken cancellationToken = default) where T : IDomainEvent;
}
