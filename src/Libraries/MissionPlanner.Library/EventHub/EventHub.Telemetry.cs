using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.Library.EventHub;

public partial class EventHub : IVehicleTelemetryEventHub, IDisposable
{
    private readonly Lock telemetryLock = new();
    private readonly Dictionary<Type, List<ITelemetrySubscription>> telemetrySubscriptions = [];
    private long droppedTelemetrySamples;
    private bool telemetryDisposed;

    long IVehicleTelemetryEventHub.DroppedSamples => Interlocked.Read(ref droppedTelemetrySamples);

    Task IVehicleTelemetryEventHub.PublishAsync<T>(T data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (telemetryLock)
        {
            if (!telemetryDisposed && telemetrySubscriptions.TryGetValue(typeof(T), out var subscriptions))
            {
                foreach (var subscription in subscriptions)
                {
                    ((TelemetrySubscription<T>)subscription).Enqueue(data, cancellationToken);
                }
            }
        }
        return Task.CompletedTask;
    }

    IDisposable IVehicleTelemetryEventHub.SubscribeAsync<T>(Func<T, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (telemetryLock)
        {
            ObjectDisposedException.ThrowIf(telemetryDisposed, this);
            var subscription = new TelemetrySubscription<T>(this, handler);
            if (!telemetrySubscriptions.TryGetValue(typeof(T), out var subscriptions))
            {
                subscriptions = [];
                telemetrySubscriptions.Add(typeof(T), subscriptions);
            }
            subscriptions.Add(subscription);
            return subscription;
        }
    }

    /// <summary>Cancels owned telemetry subscriptions when the application singleton is disposed.</summary>
    public void Dispose()
    {
        lock (telemetryLock)
        {
            if (telemetryDisposed)
            {
                return;
            }
            telemetryDisposed = true;
            foreach (var subscription in telemetrySubscriptions.Values.SelectMany(items => items).ToArray())
            {
                subscription.Dispose();
            }
            telemetrySubscriptions.Clear();
        }
        GC.SuppressFinalize(this);
    }

    private interface ITelemetrySubscription : IDisposable;

    private sealed class TelemetrySubscription<T>(EventHub owner, Func<T, CancellationToken, Task> handler)
        : ITelemetrySubscription
    {
        private readonly Queue<(T Data, CancellationToken Token)> pending = new();
        private readonly CancellationTokenSource lifetime = new();
        private bool running;
        private bool stopped;

        internal void Enqueue(T data, CancellationToken token)
        {
            // The owner lock serializes enqueue, unsubscribe and worker queue access.
            if (stopped)
            {
                return;
            }
            if (pending.Count == 256)
            {
                pending.Dequeue();
                Interlocked.Increment(ref owner.droppedTelemetrySamples);
            }
            pending.Enqueue((data, token));
            if (!running)
            {
                running = true;
                _ = Task.Run(DrainAsync);
            }
        }

        private async Task DrainAsync()
        {
            while (true)
            {
                (T Data, CancellationToken Token) item;
                lock (owner.telemetryLock)
                {
                    if (stopped || !pending.TryDequeue(out item))
                    {
                        running = false;
                        if (stopped)
                        {
                            lifetime.Dispose();
                        }
                        return;
                    }
                }
                try
                {
                    if (!item.Token.IsCancellationRequested)
                    {
                        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(item.Token, lifetime.Token);
                        await handler(item.Data, cancellation.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // An expired publisher or subscription has no more diagnostic work.
                }
                catch (Exception exception)
                {
                    owner.LogTelemetryFailure(exception);
                }
            }
        }

        public void Dispose()
        {
            lock (owner.telemetryLock)
            {
                if (stopped)
                {
                    return;
                }
                stopped = true;
                pending.Clear();
                if (owner.telemetrySubscriptions.TryGetValue(typeof(T), out var subscriptions))
                {
                    subscriptions.Remove(this);
                }
                // Cancel outside subscriber execution; cancellation callbacks must not hold the hub lock.
                _ = CancelAsync();
            }
        }

        private async Task CancelAsync()
        {
            await lifetime.CancelAsync().ConfigureAwait(false);
            lock (owner.telemetryLock)
            {
                if (!running)
                {
                    lifetime.Dispose();
                }
            }
        }
    }
}
