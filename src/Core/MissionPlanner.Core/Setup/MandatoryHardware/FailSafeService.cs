using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Implements vehicle-aware failsafe configuration using reported parameters and metadata.</summary>
public sealed class FailSafeService : MandatoryParameterServiceBase, IFailSafeService
{
    private static readonly HashSet<string> names = new(StringComparer.Ordinal)
    {
        "FS_THR_ENABLE", "FS_THR_VALUE", "FS_GCS_ENABLE", "FS_BATT_ENABLE", "FS_BATT_VOLTAGE",
        "FS_BATT_MAH", "BATT_FS_LOW_ACT", "BATT_LOW_VOLT", "BATT_LOW_MAH", "BATT_LOW_TIMER",
        "LOW_VOLT", "THR_FAILSAFE", "THR_FS_VALUE", "THR_FS_ACTION", "FS_GCS_ENABL",
        "FS_SHORT_ACTN", "FS_LONG_ACTN"
    };

    /// <summary>Initializes the failsafe service.</summary>
    public FailSafeService(
        IActiveVehicleContext activeVehicle,
        IVehicleParameterRegistry parameterRegistry,
        IVehicleParameterMetadataService metadataService,
        IVehicleParameterService parameterService)
        : base(activeVehicle, parameterRegistry, metadataService, parameterService)
    {
    }

    /// <inheritdoc />
    public async Task<MandatoryParameterConfiguration> GetConfigurationAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync(vehicleId, names.Contains, cancellationToken).ConfigureAwait(false);
        return new MandatoryParameterConfiguration(settings,
        [
            "Changing failsafe behavior can cause an automatic landing, return, disarm, or other immediate action.",
            "Verify the selected actions and thresholds with propellers removed before flight."
        ]);
    }

    /// <inheritdoc />
    public Task<MandatoryParameterApplyResult> ApplyAsync(VehicleId vehicleId, string name, double value, CancellationToken cancellationToken = default)
    {
        return base.ApplyAsync(vehicleId, name, value, Validate, cancellationToken);
    }

    private static string? Validate(string name, double value)
    {
        if (!double.IsFinite(value))
        {
            return $"{name} must be a finite value.";
        }

        if (name is "FS_THR_VALUE" or "THR_FS_VALUE" && value is < 800 or > 1200)
        {
            return $"{name} must be between 800 and 1200 PWM microseconds.";
        }

        if (name is "LOW_VOLT" or "FS_BATT_VOLTAGE" or "BATT_LOW_VOLT" && value is < 0 or > 99)
        {
            return $"{name} must be between 0 and 99 volts.";
        }

        if (name is "FS_BATT_MAH" or "BATT_LOW_MAH" && value is < 0 or > 99999)
        {
            return $"{name} must be between 0 and 99999 mAh.";
        }

        if (name == "BATT_LOW_TIMER" && value is < 0 or > 120)
        {
            return "BATT_LOW_TIMER must be between 0 and 120 seconds.";
        }

        return null;
    }
}
