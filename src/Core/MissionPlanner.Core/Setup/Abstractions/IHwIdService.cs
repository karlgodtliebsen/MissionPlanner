using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Abstractions;

/// <summary>Retrieves hardware identifiers already reported by the active vehicle.</summary>
public interface IHwIdService
{
    /// <summary>Gets a diagnostic hardware identifier snapshot.</summary>
    Task<HwIdSnapshot> GetAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);
}
