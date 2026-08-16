using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>Retains the latest parameter-loading status so late UI subscribers can catch up.</summary>
public interface IVehicleParameterLoadStatusContext
{
    /// <summary>Gets the latest status for a vehicle, if one has been published.</summary>
    ParameterLoadStatus? Get(VehicleId vehicleId);

    /// <summary>Stores the latest status for a vehicle.</summary>
    void Update(ParameterLoadStatus status);
}
