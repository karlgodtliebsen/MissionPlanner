using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>Represents normalized VTOL and landed state.</summary>
/// <param name="VtolState">The VTOL transition state.</param>
/// <param name="LandedState">The landed state.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleExtendedFlightStateObservation(VehicleVtolState VtolState, VehicleLandedState LandedState, DateTimeOffset ObservedAt) : IVehicleObservation;
