using Domain.Library.EventHub.Abstractions;
using Domain.Library.EventHub.Events;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Defines the contract for an event hub that allows subscribing to and publishing events. 
/// </summary>
public interface IDomainEventHub : IEventHub
{
    /// <summary>
    /// Subscribes For the DomainEvent specified Func signature
    /// </summary>
    /// <param name="handler"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    IDisposable SubscribeDomainEventAsync<T>(Func<T, CancellationToken, Task> handler) where T : IDomainEvent;

    /// <summary>
    /// Subscribes For the DomainEvent specified Func signature
    /// </summary>
    /// <param name="handler"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    IDisposable SubscribeDomainEvent<T>(Action<T> handler) where T : IDomainEvent;

    /// <summary>
    /// Publish DomainEvent Async
    /// </summary>
    /// <param name="data"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task PublishDomainEventAsync<T>(T data, CancellationToken cancellationToken = default) where T : IDomainEvent;


    /// <summary>
    /// Publish DomainEvent
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    void PublishDomainEvent<T>(T data) where T : IDomainEvent;
}