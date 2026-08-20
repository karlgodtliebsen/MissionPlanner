using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Abstractions;

/// <summary>Loads and applies ADS-B parameters supported by the active vehicle.</summary>
public interface IAdsbService
{
    /// <summary>Loads reported ADS-B and avoidance settings.</summary>
    Task<MandatoryParameterConfiguration> GetConfigurationAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Validates and applies one reported ADS-B setting.</summary>
    Task<MandatoryParameterApplyResult> ApplyAsync(VehicleId vehicleId, string name, double value, CancellationToken cancellationToken = default);
}
