using System.Collections.Concurrent;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles;

/// <summary>Thread-safe store for the latest connection-scoped parameter-load status.</summary>
public sealed class VehicleParameterLoadStatusContext : IVehicleParameterLoadStatusContext
{
    private readonly ConcurrentDictionary<VehicleId, ParameterLoadStatus> statuses = new();

    /// <inheritdoc />
    public ParameterLoadStatus? Get(VehicleId vehicleId) =>
        statuses.TryGetValue(vehicleId, out var status) ? status : null;

    /// <inheritdoc />
    public void Update(ParameterLoadStatus status) => statuses[status.VehicleId] = status;
}
