using Domain.Library.EventHub.Events;

namespace Domain.Library.EventHub.Abstractions;

/// <summary>
/// Defines the contract for an event hub that allows subscribing to and publishing events. 
/// </summary>
public interface IEventHub
{
    /// <summary>
    /// Subscribes to a specific event with a synchronous action.
    /// </summary>
    /// <param name="event">The name of the event to subscribe to.</param>
    /// <param name="action">The action to execute when the event is published.</param>
    /// <returns>An IDisposable that can be used to unsubscribe from the event.</returns>
    IDisposable Subscribe(string @event, Action action);

    /// <summary>
    /// Subscribes to a specific event with a synchronous action that accepts data.
    /// </summary>
    /// <param name="event">The name of the event to subscribe to.</param>
    /// <param name="action">The action to execute when the event is published.</param>
    /// <returns>An IDisposable that can be used to unsubscribe from the event.</returns>   
    IDisposable Subscribe<T>(string @event, Action<T> action);

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="action"></param>
    /// <returns></returns>
    IDisposable Subscribe<T>(Action<T> action);


    /// <summary>
    /// Publishes an event without any data.
    /// </summary>
    /// <param name="event">The name of the event to publish.</param>
    void Publish(string @event);

    /// <summary>
    /// Publishes an event with data.
    /// </summary>
    /// <param name="event">The name of the event to publish.</param>
    /// <param name="data">The data to publish with the event.</param>
    void Publish<T>(string @event, T data);


    /// <summary>
    /// Publish
    /// </summary>
    /// <param name="data"></param>
    /// <typeparam name="T"></typeparam>
    void Publish<T>(T data);


    /// <summary>
    /// Subscribes to a specific event with an asynchronous action.
    /// </summary>
    /// <param name="event">The name of the event to subscribe to.</param>
    /// <param name="action">The asynchronous action to execute when the event is published.</param>
    /// <returns>An IDisposable that can be used to unsubscribe from the event.</returns>
    IDisposable SubscribeAsync(string @event, Func<CancellationToken, Task> action);

    /// <summary>
    /// Subscribes to a specific event with an asynchronous action that accepts data.
    /// </summary>
    /// <param name="event">The name of the event to subscribe to.</param>
    /// <param name="action">The asynchronous action to execute when the event is published.</param>
    /// <returns>An IDisposable that can be used to unsubscribe from the event.</returns>
    IDisposable SubscribeAsync<T>(string @event, Func<T, CancellationToken, Task> action);

    /// <summary>
    /// Subscribes to all events of a specific type with an asynchronous action.
    /// </summary>
    /// <param name="action">The asynchronous action to execute when the event is published.</param>
    /// <returns>An IDisposable that can be used to unsubscribe from the event.</returns>
    IDisposable SubscribeAsync<T>(Func<T, CancellationToken, Task> action);

    /// <summary>
    /// Subscribes to all events with a synchronous action that accepts the event name.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    IDisposable SubscribeAll(Action<string> action);

    /// <summary>
    /// Subscribes to all events with an asynchronous action that accepts the event name.
    /// </summary>
    /// <param name="event"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task PublishAsync(string @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an event with data asynchronously.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="event"></param>
    /// <param name="data"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task PublishAsync<T>(string @event, T data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an event with data asynchronously to all subscribers of that data type, regardless of the event name.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="data"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task PublishAsync<T>(T data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an event with data asynchronously to all subscribers for the eventName, and broadcast to @event.
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="event"></param>
    /// <param name="data"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    Task PublishAsync<T>(string eventName, string @event, T data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to specified eventName for th specified Func signature
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    IDisposable SubscribeAsync(string eventName, Func<IDomainEvent, CancellationToken, Task> action);
}
