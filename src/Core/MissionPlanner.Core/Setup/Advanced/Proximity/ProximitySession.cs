using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Services;

namespace MissionPlanner.Core.Setup.Advanced.Proximity;

/// <summary>Observes proximity on the selected vehicle's existing connection without requesting or changing stream rates.</summary>
public sealed class ProximitySession(IVehicleConnectionSession connection, IActiveVehicleContext vehicle,
    ProximityAggregator aggregator, TimeProvider clock)
{
    private MavLinkInspectionLease? lease;
    private CancellationTokenSource? cancellation;
    private Task reader = Task.CompletedTask;

    /// <summary>Starts a bounded observer scoped to the current active vehicle and connection.</summary>
    public void Start(CancellationToken token)
    {
        if (lease is not null)
        {
            return;
        }
        if (!vehicle.IsOnline || vehicle.VehicleId is null)
        {
            throw new InvalidOperationException("Select a connected vehicle before opening Proximity.");
        }
        var tap = connection.Connection.Inspection ?? throw new NotSupportedException("This connection has no proximity observation tap.");
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, vehicle.ConnectionCancellationToken);
        lease = tap.Subscribe(256);
        aggregator.Clear();
        reader = ReadAsync(lease, vehicle.VehicleId.Value.SystemId, cancellation.Token);
    }

    /// <summary>Gets the normalized snapshot; disconnected data is cleared rather than shown as current.</summary>
    public ProximitySnapshot Snapshot()
    {
        if (cancellation?.IsCancellationRequested == true)
        {
            aggregator.Clear();
        }
        return aggregator.Snapshot(lease?.Dropped ?? 0);
    }

    /// <summary>Stops the observer and clears all retained sensor data.</summary>
    public async Task StopAsync()
    {
        cancellation?.Cancel();
        lease?.Dispose();
        try
        {
            await reader.ConfigureAwait(false);
        }
        finally
        {
            cancellation?.Dispose();
            cancellation = null;
            lease = null;
            aggregator.Clear();
        }
    }

    private async Task ReadAsync(MavLinkInspectionLease observer, byte systemId, CancellationToken token)
    {
        try
        {
            await foreach (var observation in observer.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                if (observation.Direction != MavLinkTrafficDirection.Inbound || !observation.CrcVerified
                    || observation.Message is null || observation.Frame.SystemId != systemId)
                {
                    continue;
                }
                var position = vehicle.State?.Position;
                var age = position?.ObservedAt is { } observed ? clock.GetUtcNow() - observed : TimeSpan.MaxValue;
                var heading = age >= TimeSpan.Zero && age <= TimeSpan.FromSeconds(2) ? position?.HeadingDegrees : null;
                aggregator.Observe(observation.Message, heading);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }
}
