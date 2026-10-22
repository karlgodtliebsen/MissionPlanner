using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>Owns session-local relative-altitude display zeroes.</summary>
public interface ILocalAltitudeReferenceService
{
    /// <summary>Occurs when the local altitude reference of a vehicle changes.</summary>
    event EventHandler<LocalAltitudeReferenceChangedEventArgs>? Changed;
    /// <summary>Gets whether a local altitude reference exists for the vehicle.</summary>
    bool HasReference(VehicleId vehicleId);
    /// <summary>Gets the local altitude reference of the vehicle.</summary>
    bool TryGetReference(VehicleId vehicleId, out double referenceMeters);
    /// <summary>Sets the local reference so the current relative altitude reads as zero.</summary>
    bool TryZero(VehicleId vehicleId, double relativeAltitudeMeters);
    /// <summary>Removes the local altitude reference.</summary>
    void Reset(VehicleId vehicleId);
}

/// <summary>Identifies the vehicle whose local display reference changed.</summary>
public sealed class LocalAltitudeReferenceChangedEventArgs(VehicleId vehicleId) : EventArgs
{
    /// <summary>Gets the vehicle whose reference changed.</summary>
    public VehicleId VehicleId { get; } = vehicleId;
}
