using System.Collections.Concurrent;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles;

/// <inheritdoc />
public sealed class LocalAltitudeReferenceService : ILocalAltitudeReferenceService, IDisposable
{
    private readonly ConcurrentDictionary<VehicleId, double> references = new();
    private readonly IDisposable registeredSubscription;
    private readonly IDisposable disconnectedSubscription;

    public LocalAltitudeReferenceService(IDomainEventHub eventHub)
    {
        registeredSubscription = eventHub.SubscribeDomainEventAsync<VehicleRegistered>((evt, _) =>
        {
            Reset(evt.VehicleId);
            return Task.CompletedTask;
        });
        disconnectedSubscription = eventHub.SubscribeDomainEventAsync<VehicleDisconnected>((evt, _) =>
        {
            Reset(evt.VehicleId);
            return Task.CompletedTask;
        });
    }

    public event EventHandler<LocalAltitudeReferenceChangedEventArgs>? Changed;
    public bool HasReference(VehicleId vehicleId) => references.ContainsKey(vehicleId);
    public bool TryGetReference(VehicleId vehicleId, out double referenceMeters) => references.TryGetValue(vehicleId, out referenceMeters);

    public bool TryZero(VehicleId vehicleId, double relativeAltitudeMeters)
    {
        if (!double.IsFinite(relativeAltitudeMeters))
        {
            return false;
        }
        references[vehicleId] = relativeAltitudeMeters;
        Changed?.Invoke(this, new LocalAltitudeReferenceChangedEventArgs(vehicleId));
        return true;
    }

    public void Reset(VehicleId vehicleId)
    {
        if (references.TryRemove(vehicleId, out _))
        {
            Changed?.Invoke(this, new LocalAltitudeReferenceChangedEventArgs(vehicleId));
        }
    }

    public void Dispose()
    {
        registeredSubscription.Dispose();
        disconnectedSubscription.Dispose();
    }
}
