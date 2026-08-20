using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Abstractions;

/// <summary>Loads and applies supported initial-tune parameters.</summary>
public interface IInitTuneParametersService
{
    /// <summary>Loads the active vehicle's supported initial-tune settings.</summary>
    Task<MandatoryParameterConfiguration> GetConfigurationAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Validates and applies one reported initial-tune setting.</summary>
    Task<MandatoryParameterApplyResult> ApplyAsync(VehicleId vehicleId, string name, double value, CancellationToken cancellationToken = default);
}
