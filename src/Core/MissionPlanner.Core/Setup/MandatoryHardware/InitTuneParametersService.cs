using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Provides metadata-backed access to initial tune parameters reported by Copter and QuadPlane firmware.</summary>
public sealed class InitTuneParametersService : MandatoryParameterServiceBase, IInitTuneParametersService
{
    private static readonly string[] prefixes = ["ACRO_", "ATC_", "Q_A_", "INS_ACCEL_FILTER", "INS_GYRO_FILTER", "MOT_", "Q_M_", "BATT_ARM_VOLT", "BATT_CRT_VOLT", "BATT_LOW_VOLT"];

    /// <summary>Initializes the initial-tune parameter service.</summary>
    public InitTuneParametersService(IActiveVehicleContext activeVehicle, IVehicleParameterRegistry registry, IVehicleParameterMetadataService metadata, IVehicleParameterService parameters)
        : base(activeVehicle, registry, metadata, parameters)
    {
    }

    /// <inheritdoc />
    public async Task<MandatoryParameterConfiguration> GetConfigurationAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync(vehicleId, IsRelevant, cancellationToken).ConfigureAwait(false);
        return new MandatoryParameterConfiguration(settings,
        [
            "Initial tune values materially affect flight behavior. Apply only reviewed values and test with appropriate safety precautions.",
            "The calculator preserves the original MissionPlanner propeller and battery formulas; only parameters reported by this firmware are shown."
        ]);
    }

    /// <inheritdoc />
    public Task<MandatoryParameterApplyResult> ApplyAsync(VehicleId vehicleId, string name, double value, CancellationToken cancellationToken = default)
    {
        return base.ApplyAsync(vehicleId, name, value, Validate, cancellationToken);
    }

    private static bool IsRelevant(string name)
    {
        return prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)) &&
            (name.Contains("ACCEL", StringComparison.Ordinal) || name.Contains("FILTER", StringComparison.Ordinal) ||
             name.Contains("FLTD", StringComparison.Ordinal) || name.Contains("FLTE", StringComparison.Ordinal) ||
             name.Contains("FLTT", StringComparison.Ordinal) || name.Contains("THST", StringComparison.Ordinal) ||
             name.Contains("BAT_VOLT", StringComparison.Ordinal) || name.StartsWith("BATT_", StringComparison.Ordinal) ||
             name == "ACRO_YAW_P" || name.EndsWith("THR_MIX_MAN", StringComparison.Ordinal));
    }

    private static string? Validate(string name, double value)
    {
        return double.IsFinite(value) && value >= 0
            ? null
            : $"{name} must be a finite non-negative value.";
    }
}
