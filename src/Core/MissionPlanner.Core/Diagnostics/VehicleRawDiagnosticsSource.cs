using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Simulation.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Diagnostics;

/// <summary>Normalizes bounded connection inspection streams without blocking receive or depending on UI.</summary>
public sealed class VehicleRawDiagnosticsSource : IDisposable
{
    private readonly Lock sync = new();
    private readonly Dictionary<VehicleId, MavLinkInspectionLease> leases = [];
    private readonly List<IDisposable> subscriptions = [];
    private readonly CancellationTokenSource lifetime = new();
    private readonly IVehicleTelemetryEventHub telemetry;
    private readonly IMavLinkMessageDefinitionRegistry definitions;
    private readonly ILogger<VehicleRawDiagnosticsSource> logger;

    /// <summary>Observes connections using the existing application connection boundary.</summary>
    public VehicleRawDiagnosticsSource(IDomainEventHub domain, IVehicleTelemetryEventHub telemetry,
        IVehicleConnectionSession session, IMavLinkMessageDefinitionRegistry definitions,
        ILogger<VehicleRawDiagnosticsSource> logger, ISimulationVehicleChannelRegistry? simulations = null)
    {
        this.telemetry = telemetry;
        this.definitions = definitions;
        this.logger = logger;
        subscriptions.Add(domain.SubscribeDomainEventAsync<VehicleConnected>((item, cancellationToken) =>
        {
            var connection = simulations?.Find(item.VehicleId)?.ConnectionSession ?? session;
            lock (sync)
            {
                if (leases.Remove(item.VehicleId, out var previous))
                {
                    previous.Dispose();
                }
                if (connection.Connection.Inspection is { } tap)
                {
                    var lease = tap.Subscribe(512);
                    leases.Add(item.VehicleId, lease);
                    _ = Task.Run(() => ReadAsync(item.VehicleId, lease));
                }
            }
            return Task.CompletedTask;
        }));
        subscriptions.Add(domain.SubscribeDomainEventAsync<VehicleDisconnected>((item, _) =>
        {
            lock (sync)
            {
                if (leases.Remove(item.VehicleId, out var lease))
                {
                    lease.Dispose();
                }
            }
            return Task.CompletedTask;
        }));
    }

    private async Task ReadAsync(VehicleId vehicle, MavLinkInspectionLease lease)
    {
        try
        {
            await foreach (var item in lease.Reader.ReadAllAsync(lifetime.Token).ConfigureAwait(false))
            {
                var frame = item.Frame;
                if (item.Direction != MavLinkTrafficDirection.Inbound || frame.SystemId != vehicle.SystemId)
                {
                    continue;
                }
                definitions.TryGet(frame.MessageId, out var definition);
                await telemetry.PublishAsync(VehicleRawDiagnostic.Capture(vehicle, item, definition),
                    lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Raw diagnostic observation stopped for {VehicleId}", vehicle);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }
        lock (sync)
        {
            foreach (var lease in leases.Values)
            {
                lease.Dispose();
            }
            leases.Clear();
        }
        lifetime.Cancel();
    }
}
