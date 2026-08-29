using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>Owns session-local relative-altitude display zeroes.</summary>
public interface ILocalAltitudeReferenceService
{
    event EventHandler<LocalAltitudeReferenceChangedEventArgs>? Changed;
    bool HasReference(VehicleId vehicleId);
    bool TryGetReference(VehicleId vehicleId, out double referenceMeters);
    bool TryZero(VehicleId vehicleId, double relativeAltitudeMeters);
    void Reset(VehicleId vehicleId);
}

/// <summary>Identifies the vehicle whose local display reference changed.</summary>
public sealed class LocalAltitudeReferenceChangedEventArgs(VehicleId vehicleId) : EventArgs
{
    public VehicleId VehicleId { get; } = vehicleId;
}
