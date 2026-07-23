using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Maintains the application-wide active vehicle from vehicle lifecycle domain events.
/// </summary>
public sealed class ActiveVehicleContext : IActiveVehicleContext, IDisposable
{
    private readonly object sync = new();
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly List<IDisposable> subscriptions = [];
    private ActiveVehicleSnapshot current = ActiveVehicleSnapshot.Empty;
    private CancellationTokenSource connectionLifetime = new();
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveVehicleContext"/> class.
    /// </summary>
    /// <param name="eventHub">The vehicle lifecycle event hub.</param>
    /// <param name="vehicleRegistry">The registry containing immutable vehicle snapshots.</param>
    public ActiveVehicleContext(IDomainEventHub eventHub, IVehicleRegistry vehicleRegistry)
    {
        this.vehicleRegistry = vehicleRegistry;
        connectionLifetime.Cancel();
        subscriptions.Add(eventHub.SubscribeDomainEventAsync<VehicleConnected>(OnConnectedAsync));
        subscriptions.Add(eventHub.SubscribeDomainEventAsync<VehicleDisconnected>(OnDisconnectedAsync));
        subscriptions.Add(eventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnStateUpdatedAsync));
        subscriptions.Add(eventHub.SubscribeDomainEventAsync<VehicleRegistryReset>(OnRegistryResetAsync));
    }

    /// <inheritdoc />
    public ActiveVehicleSnapshot Current
    {
        get
        {
            lock (sync)
            {
                return current;
            }
        }
    }

    /// <inheritdoc />
    public VehicleId? VehicleId => Current.VehicleId;

    /// <inheritdoc />
    public VehicleState? State => Current.State;

    /// <inheritdoc />
    public bool IsOnline => Current.IsOnline;

    /// <inheritdoc />
    public CancellationToken ConnectionCancellationToken
    {
        get
        {
            lock (sync)
            {
                return connectionLifetime.Token;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<ActiveVehicleChangedEventArgs>? Changed;

    /// <inheritdoc />
    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            connectionLifetime.Cancel();
            connectionLifetime.Dispose();
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }

        subscriptions.Clear();
    }

    private Task OnConnectedAsync(VehicleConnected evt, CancellationToken cancellationToken)
    {
        var state = vehicleRegistry.GetRequired(evt.VehicleId)?.State;
        SetCurrent(new ActiveVehicleSnapshot(evt.VehicleId, state));
        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(VehicleDisconnected evt, CancellationToken cancellationToken)
    {
        var snapshot = Current;
        if (snapshot.VehicleId != evt.VehicleId)
        {
            return Task.CompletedTask;
        }

        var offlineState = snapshot.State is null
            ? null
            : snapshot.State with { Connection = snapshot.State.Connection with { State = VehicleConnectionState.Offline } };
        SetCurrent(new ActiveVehicleSnapshot(evt.VehicleId, offlineState));
        return Task.CompletedTask;
    }

    private Task OnStateUpdatedAsync(VehicleStateUpdated evt, CancellationToken cancellationToken)
    {
        var snapshot = Current;
        if (snapshot.VehicleId == evt.VehicleId)
        {
            SetCurrent(new ActiveVehicleSnapshot(evt.VehicleId, evt.VehicleState));
        }

        return Task.CompletedTask;
    }

    private Task OnRegistryResetAsync(VehicleRegistryReset evt, CancellationToken cancellationToken)
    {
        var snapshot = Current;
        if (snapshot.VehicleId is null)
        {
            return Task.CompletedTask;
        }

        var offlineState = snapshot.State is null
            ? null
            : snapshot.State with { Connection = snapshot.State.Connection with { State = VehicleConnectionState.Offline } };
        SetCurrent(new ActiveVehicleSnapshot(snapshot.VehicleId, offlineState));
        return Task.CompletedTask;
    }

    private void SetCurrent(ActiveVehicleSnapshot next)
    {
        ActiveVehicleSnapshot previous;
        CancellationTokenSource? lifetimeToCancel = null;
        var connectionBoundaryChanged = false;
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            if (current == next)
            {
                return;
            }

            previous = current;
            connectionBoundaryChanged = previous.VehicleId != next.VehicleId || previous.IsOnline != next.IsOnline;
            current = next;
            if (connectionBoundaryChanged)
            {
                lifetimeToCancel = connectionLifetime;
                connectionLifetime = new CancellationTokenSource();
                if (!next.IsOnline)
                {
                    connectionLifetime.Cancel();
                }
            }
        }

        lifetimeToCancel?.Cancel();
        lifetimeToCancel?.Dispose();
        if (connectionBoundaryChanged)
        {
            Changed?.Invoke(this, new ActiveVehicleChangedEventArgs(previous, next));
        }
    }
}
