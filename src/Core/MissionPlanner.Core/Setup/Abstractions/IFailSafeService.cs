using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Abstractions;

/// <summary>Loads and applies the failsafe parameters supported by the active vehicle.</summary>
public interface IFailSafeService
{
    /// <summary>Loads the active vehicle's reported failsafe settings.</summary>
    Task<MandatoryParameterConfiguration> GetConfigurationAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Validates and applies one reported failsafe setting.</summary>
    Task<MandatoryParameterApplyResult> ApplyAsync(VehicleId vehicleId, string name, double value, CancellationToken cancellationToken = default);
}
