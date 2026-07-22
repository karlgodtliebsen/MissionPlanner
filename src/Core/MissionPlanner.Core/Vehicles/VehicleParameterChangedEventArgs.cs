using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Vehicles;

/// <summary>Provides details when a vehicle parameter registry changes.</summary>
/// <param name="vehicleId">The affected vehicle.</param>
/// <param name="parameter">The changed parameter, or <see langword="null"/> when the vehicle set was cleared.</param>
public sealed class VehicleParameterChangedEventArgs(VehicleId vehicleId, VehicleParameter? parameter) : EventArgs
{
    /// <summary>Gets the affected vehicle.</summary>
    public VehicleId VehicleId { get; } = vehicleId;

    /// <summary>Gets the changed parameter, or <see langword="null"/> for a clear operation.</summary>
    public VehicleParameter? Parameter { get; } = parameter;
}
