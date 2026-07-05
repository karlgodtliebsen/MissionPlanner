using System.Collections.Concurrent;

using Domain.Library.EventHub.Abstractions;
using Domain.Library.EventHub.DisposableWrappers;
using Domain.Library.EventHub.Events;

using Microsoft.CSharp.RuntimeBinder;
using Microsoft.Extensions.Logging;

namespace Domain.Library.EventHub;

/// <summary>
/// 
/// </summary>
/// <param name="logger"></param>
public class EventHub(ILogger<EventHub> logger) : IEventHub
{
    private readonly ConcurrentDictionary<string, IList<Action>> subscribers = new();
    private readonly ConcurrentDictionary<string, IList<Delegate>> genericDataSubscribers = new();
    private readonly ConcurrentDictionary<string, IList<Func<CancellationToken, Task>>> asyncSubscribers = new();
    private readonly ConcurrentDictionary<string, IList<object>> asyncGenericDataSubscribers = new();
    private readonly List<Action<string>> allSubscribers = [];
    private readonly Lock lockObj = new();

    internal void Unsubscribe(string @event, Func<CancellationToken, Task> action)
    {
        if (asyncSubscribers.TryGetValue(@event, out var list))
        {
            using (lockObj.EnterScope())
            {
                list.Remove(action);
                if (list.Count == 0)
                {
                    asyncSubscribers.TryRemove(@event, out var _);
                }
            }
        }
    }

    internal void Unsubscribe<T>(string @event, Func<T, CancellationToken, Task> action)
    {
        var key = KeyGenerator.GetEventKey<T>(@event);
        if (asyncGenericDataSubscribers.TryGetValue(key, out var list))
        {
            using (lockObj.EnterScope())
            {
                list.Remove(action);
                if (list.Count == 0)
                {
                    asyncGenericDataSubscribers.TryRemove(key, out var _);
                }
            }
        }
    }

    internal void Unsubscribe(string @event, Action action)
    {
        if (subscribers.TryGetValue(@event, out var list))
        {
            using (lockObj.EnterScope())
            {
                list.Remove(action);
                if (list.Count == 0)
                {
                    subscribers.TryRemove(@event, out var _);
                }
            }
        }
    }

    internal void Unsubscribe<T>(string @event, Action<T> action)
    {
        var key = KeyGenerator.GetEventKey<T>(@event);
        if (genericDataSubscribers.TryGetValue(key, out var list))
        {
            using (lockObj.EnterScope())
            {
                list.Remove(action);
                if (list.Count == 0)
                {
                    genericDataSubscribers.TryRemove(key, out var _);
                }
            }
        }
    }

    internal void UnsubscribeFromAll(Action<string> action)
    {
        using (lockObj.EnterScope())
        {
            if (allSubscribers.Contains(action))
            {
                allSubscribers.Remove(action);
            }
        }
    }

    /// <inheritdoc/>
    public IDisposable SubscribeAsync(string @event, Func<CancellationToken, Task> action)
    {
        asyncSubscribers.AddOrUpdate(
            @event,
            [action],
            (key, list) =>
            {
                using (lockObj.EnterScope())
                {
                    list.Add(action);
                    return list;
                }
            });
        return new EventAsyncSubscriptionWithActionDispose(this, @event, action);
    }

    /// <inheritdoc/>
    public IDisposable SubscribeAsync<T>(string @event, Func<T, CancellationToken, Task> handler)
    {
        var key = KeyGenerator.GetEventKey<T>(@event);
        asyncGenericDataSubscribers.AddOrUpdate(
            key,
            [handler],
            (_, list) =>
            {
                using (lockObj.EnterScope())
                {
                    list.Add(handler);
                    return list;
                }
            });
        return new EventAsyncSubscriptionGenericDispose<T>(this, @event, handler);
    }

    /// <inheritdoc/>
    public IDisposable SubscribeAsync<T>(Func<T, CancellationToken, Task> handler)
    {
        var key = KeyGenerator.GetEventKey<T>();
        asyncGenericDataSubscribers.AddOrUpdate(
            key,
            [handler],
            (_, list) =>
            {
                using (lockObj.EnterScope())
                {
                    list.Add(handler);
                    return list;
                }
            });
        return new EventAsyncSubscriptionGenericDispose<T>(this, key, handler);
    }


    /// <inheritdoc/>
    public IDisposable SubscribeAll(Action<string> action)
    {
        using (lockObj.EnterScope())
        {
            allSubscribers.Add(action);
        }

        return new EventSubscriptionWithStringActionDispose(this, action);
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(string @event, Action action)
    {
        subscribers.AddOrUpdate(
            @event,
            [action],
            (_, list) =>
            {
                using (lockObj.EnterScope())
                {
                    list.Add(action);
                }

                return list;
            });
        return new EventSubscriptionWithActionDispose(this, @event, action);
    }

    /// <inheritdoc/>
    public IDisposable Subscribe<T>(string @event, Action<T> action)
    {
        var key = KeyGenerator.GetEventKey<T>(@event);
        genericDataSubscribers.AddOrUpdate(
            key,
            [action],
            (_, list) =>
            {
                using (lockObj.EnterScope())
                {
                    list.Add(action);
                }

                return list;
            });
        return new EventSubscriptionGenericDispose<T>(this, @event, action);
    }

    /// <inheritdoc />
    public IDisposable Subscribe<T>(Action<T> action)
    {
        var @event = nameof(T);
        var key = KeyGenerator.GetEventKey<T>(@event);
        genericDataSubscribers.AddOrUpdate(
            key,
            [action],
            (_, list) =>
            {
                using (lockObj.EnterScope())
                {
                    list.Add(action);
                }

                return list;
            });
        return new EventSubscriptionGenericDispose<T>(this, @event, action);
    }

    private void BroadCastEvent(string @event)
    {
        List<Action<string>> actionsSnapshot;
        using (lockObj.EnterScope())
        {
            actionsSnapshot = [.. allSubscribers];
        }

        foreach (var action in actionsSnapshot)
        {
            action.Invoke(@event);
        }
    }

    /// <inheritdoc/>
    public void Publish<T>(T data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var @event = nameof(T);
        var key = KeyGenerator.GetEventKey<T>(@event);

        if (genericDataSubscribers.TryGetValue(key, out var actions))
        {
            List<Delegate> actionsSnapshot;
            using (lockObj.EnterScope())
            {
                actionsSnapshot = [.. actions];
            }

            foreach (var action in actionsSnapshot)
            {
                if (action is Action<T> typedAction)
                {
                    typedAction.Invoke(data);
                }
            }
        }

        BroadCastEvent(key);
    }

    /// <inheritdoc/>
    public void Publish<T>(string @event, T data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var key = KeyGenerator.GetEventKey<T>(@event);

        if (genericDataSubscribers.TryGetValue(key, out var actions))
        {
            List<Delegate> actionsSnapshot;
            using (lockObj.EnterScope())
            {
                actionsSnapshot = [.. actions];
            }

            foreach (var action in actionsSnapshot)
            {
                if (action is Action<T> typedAction)
                {
                    typedAction.Invoke(data);
                }
            }
        }

        BroadCastEvent(key);
    }

    /// <inheritdoc/>
    public void Publish(string @event)
    {
        if (subscribers.TryGetValue(@event, out var actions))
        {
            List<Action> actionsSnapshot;
            using (lockObj.EnterScope())
            {
                actionsSnapshot = [.. actions];
            }

            foreach (var action in actionsSnapshot)
            {
                action.Invoke();
            }
        }

        BroadCastEvent(@event);
    }

    /// <inheritdoc/>
    public async Task PublishAsync(string @event, CancellationToken cancellationToken = default)
    {
        if (asyncSubscribers.TryGetValue(@event, out var actions))
        {
            List<Func<CancellationToken, Task>> actionsSnapshot;
            using (lockObj.EnterScope())
            {
                actionsSnapshot = [.. actions];
            }

            foreach (var action in actionsSnapshot)
            {
                await action.Invoke(cancellationToken);
            }
        }

        BroadCastEvent(@event);
    }


    /// <inheritdoc/>
    public async Task PublishAsync<T>(T data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        var key = KeyGenerator.GetEventKey<T>();
        await PublishAsync<T>(key, key, data, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task PublishAsync<T>(string @event, T data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        var key = KeyGenerator.GetEventKey<T>(@event);
        await PublishAsync<T>(key, @event, data, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task PublishAsync<T>(string eventName, string @event, T data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (asyncGenericDataSubscribers.TryGetValue(eventName, out var actions))
        {
            List<object> actionsSnapshot;
            using (lockObj.EnterScope())
            {
                actionsSnapshot = [.. actions];
            }

            foreach (var action in actionsSnapshot)
            {
                try
                {
                    if (action is Func<T, CancellationToken, Task> typedAction)
                    {
                        await typedAction.Invoke(data, cancellationToken);
                    }
                    else
                    {
                        await ((dynamic)action).Invoke((dynamic)data, cancellationToken);
                    }
                }
                catch (RuntimeBinderException ex)
                {
                    logger.LogError(ex, "Action invocation failed due to type mismatch");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Action invocation failed");
                }
            }
        }

        BroadCastEvent(@event);
    }

    /// <inheritdoc/>
    public IDisposable SubscribeAsync(string eventName, Func<IDomainEvent, CancellationToken, Task> handler)
    {
        return SubscribeAsync<IDomainEvent>(eventName, handler);
    }
}
