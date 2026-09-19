using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles;

/// <summary>Monitors valid frame activity and transport termination independently of the UI.</summary>
public sealed class VehicleConnectionMonitor(
    IVehicleRegistry registry,
    IDomainEventHub events,
    TimeProvider clock,
    IOptions<VehicleConnectionHealthOptions> options,
    ILogger<VehicleConnectionMonitor> logger,
    IUserNotificationService? notifications = null) : IVehicleConnectionMonitor, IAsyncDisposable, IDisposable
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, Watch> watches = [];

    /// <inheritdoc />
    public IDisposable? Track(VehicleId vehicleId, Guid connectionId, IVehicleConnectionSession session,
        Func<string, Task> disconnect)
    {
        var activity = session.Connection.Activity;
        if (activity is null)
        {
            return null;
        }
        options.Value.Validate();
        var watch = new Watch(vehicleId, connectionId, session.ActiveTransportProtocol, activity, disconnect, clock.GetTimestamp());
        if (!watches.TryAdd(connectionId, watch))
        {
            throw new InvalidOperationException("Connection already monitored.");
        }
        watch.Work = Task.Run(() => RunAsync(watch));
        return watch;
    }

    private async Task RunAsync(Watch watch)
    {
        await Task.Yield();
        try
        {
            using var timer = new PeriodicTimer(options.Value.PollInterval, clock);
            while (!watch.Cancellation.IsCancellationRequested)
            {
                await EvaluateAsync(watch).ConfigureAwait(false);
                if (watch.Disconnected || watch.Cancellation.IsCancellationRequested)
                {
                    break;
                }
                var tick = timer.WaitForNextTickAsync(watch.Cancellation.Token).AsTask();
                await Task.WhenAny(tick, watch.Activity.Ended).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (watch.Cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Connection monitoring failed for {VehicleId} {ConnectionId}", watch.VehicleId, watch.ConnectionId);
            if (!watch.Disconnected && !watch.Cancellation.IsCancellationRequested)
            {
                watch.Disconnected = true;
                watch.Activity.End("MonitoringFault");
                await watch.Disconnect("MonitoringFault").ConfigureAwait(false);
            }
        }
        finally
        {
            watches.TryRemove(watch.ConnectionId, out _);
        }
    }

    /// <inheritdoc />
    public async Task UpdateConnectionStatesAsync(CancellationToken cancellationToken)
    {
        foreach (var watch in watches.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EvaluateAsync(watch).ConfigureAwait(false);
        }
    }

    private async Task EvaluateAsync(Watch watch)
    {
        await watch.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (watch.Disconnected || watch.Cancellation.IsCancellationRequested)
            {
                return;
            }
            var vehicle = registry.GetRequired(watch.VehicleId);
            if (vehicle is null)
            {
                return;
            }
            var age = watch.Activity.PacketAge(watch.VehicleId.SystemId) ?? clock.GetElapsedTime(watch.Started);
            var reason = watch.Activity.Ended.IsCompletedSuccessfully ? watch.Activity.Ended.Result
                : age >= options.Value.DisconnectedAfter ? "CommunicationTimeout" : null;
            var current = reason is not null ? VehicleConnectionState.Offline
                : age >= options.Value.DegradedAfter ? VehicleConnectionState.Degraded : VehicleConnectionState.Online;
            var armed = vehicle.State.IsArmed;
            var warning = armed && current != VehicleConnectionState.Online
                ? $"CONNECTION LOST · {age.TotalSeconds:F1}s since valid data" : null;
            var previous = watch.State;
            watch.State = current;
            lock (vehicle)
            {
                vehicle.ApplyConnectionHealth(current, watch.Activity.LastPacketAt(watch.VehicleId.SystemId), reason, warning);
            }
            try
            {
                await events.PublishDomainEventAsync(new VehicleStateUpdated(vehicle.State)).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Vehicle-state event failed for {VehicleId}", watch.VehicleId);
            }
            if (previous != current)
            {
                logger.LogInformation("Connection {PreviousState} -> {State}: {VehicleId} {ConnectionId} {Transport}, LastPacketAge={LastPacketAge}, LastHeartbeatAge={LastHeartbeatAge}, DisconnectReason={DisconnectReason}, IsArmed={IsArmed}",
                    previous, current, watch.VehicleId, watch.ConnectionId, watch.Transport, age,
                    clock.GetUtcNow() - vehicle.State.Connection.LastHeartbeatAt, reason, armed);
                try
                {
                    await events.PublishDomainEventAsync(new VehicleConnectionStateChanged(
                        new(watch.VehicleId, previous, current, clock.GetUtcNow()))).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Connection-state event failed for {VehicleId}", watch.VehicleId);
                }
            }
            if (current == VehicleConnectionState.Online)
            {
                watch.LastWarning = null;
            }
            else if (armed && clock.GetElapsedTime(watch.Started) >= options.Value.NotificationGracePeriod &&
                (watch.LastWarning is null || clock.GetElapsedTime(watch.LastWarning.Value) >= options.Value.WarningRepeatInterval))
            {
                watch.LastWarning = clock.GetTimestamp();
                if (notifications is not null)
                {
                    // Notification failures must never prevent disconnect cleanup.
                    try
                    {
                        await notifications.NotifyAsync(new UserNotification(warning!, "Armed vehicle connection lost",
                            UserNotificationSeverity.Error, VehicleId: watch.VehicleId), watch.Cancellation.Token).ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        logger.LogDebug(exception, "Unable to present connection warning for {VehicleId}", watch.VehicleId);
                    }
                }
            }
            if (reason is not null)
            {
                watch.Disconnected = true;
                watch.Activity.End(reason);
                await watch.Disconnect(reason).ConfigureAwait(false);
            }
        }
        finally
        {
            watch.Gate.Release();
        }
    }

    /// <summary>Stops monitoring when a synchronous service scope ends.</summary>
    public void Dispose()
    {
        foreach (var watch in watches.Values)
        {
            watch.Dispose();
        }
    }

    /// <summary>Stops and awaits monitor tasks during shutdown.</summary>
    public async ValueTask DisposeAsync()
    {
        var active = watches.Values.ToArray();
        foreach (var watch in active)
        {
            watch.Dispose();
        }
        await Task.WhenAll(active.Select(watch => watch.Work)).ConfigureAwait(false);
    }

    private sealed class Watch(VehicleId vehicleId, Guid connectionId, string? transport,
        MissionPlanner.MavLink.Services.MavLinkConnectionActivity activity, Func<string, Task> disconnect, long started) : IDisposable
    {
        public VehicleId VehicleId { get; } = vehicleId;
        public Guid ConnectionId { get; } = connectionId;
        public string? Transport { get; } = transport;
        public MissionPlanner.MavLink.Services.MavLinkConnectionActivity Activity { get; } = activity;
        public Func<string, Task> Disconnect { get; } = disconnect;
        public long Started { get; } = started;
        public CancellationTokenSource Cancellation { get; } = new();
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public Task Work { get; set; } = Task.CompletedTask;
        public VehicleConnectionState State { get; set; } = VehicleConnectionState.Online;
        public long? LastWarning { get; set; }
        public bool Disconnected { get; set; }
        public void Dispose()
        {
            Cancellation.Cancel();
        }
    }
}
