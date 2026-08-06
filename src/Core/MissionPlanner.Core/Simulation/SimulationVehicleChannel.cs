using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Simulation;

namespace MissionPlanner.Core.Simulation;

/// <summary>Associates one simulator session and vehicle with its exact MAVLink connection services.</summary>
/// <param name="SessionId">Owning simulation session identity.</param>
/// <param name="VehicleId">Exact connected vehicle identity.</param>
/// <param name="ConnectionSession">Independently owned connection session.</param>
/// <param name="Profile">Allocated simulator profile.</param>
/// <param name="StartedAt">Connection readiness timestamp.</param>
public sealed record SimulationVehicleChannel(Guid SessionId, VehicleId VehicleId, IVehicleConnectionSession ConnectionSession, SimulatorProfile Profile, DateTimeOffset StartedAt);
