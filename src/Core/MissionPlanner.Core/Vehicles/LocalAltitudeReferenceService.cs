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

    /// <summary>Creates the service and clears references when a vehicle registers again or disconnects.</summary>
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

    /// <summary>Occurs when the local altitude reference of a vehicle changes.</summary>
    public event EventHandler<LocalAltitudeReferenceChangedEventArgs>? Changed;
    /// <summary>Gets whether a local altitude reference exists for the vehicle.</summary>
    public bool HasReference(VehicleId vehicleId) => references.ContainsKey(vehicleId);
    /// <summary>Gets the local altitude reference of the vehicle.</summary>
    public bool TryGetReference(VehicleId vehicleId, out double referenceMeters) => references.TryGetValue(vehicleId, out referenceMeters);

    /// <summary>Sets the local reference so the current relative altitude reads as zero.</summary>
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

    /// <summary>Removes the local altitude reference.</summary>
    public void Reset(VehicleId vehicleId)
    {
        if (references.TryRemove(vehicleId, out _))
        {
            Changed?.Invoke(this, new LocalAltitudeReferenceChangedEventArgs(vehicleId));
        }
    }

    /// <summary>Releases the event subscriptions.</summary>
    public void Dispose()
    {
        registeredSubscription.Dispose();
        disconnectedSubscription.Dispose();
    }
}
