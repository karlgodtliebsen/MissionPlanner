using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Provides capability-aware ADS-B configuration from reported ADSB and avoidance parameters.</summary>
public sealed class AdsbService : MandatoryParameterServiceBase, IAdsbService
{
    /// <summary>Initializes the ADS-B service.</summary>
    public AdsbService(IActiveVehicleContext activeVehicle, IVehicleParameterRegistry registry, IVehicleParameterMetadataService metadata, IVehicleParameterService parameters)
        : base(activeVehicle, registry, metadata, parameters)
    {
    }

    /// <inheritdoc />
    public async Task<MandatoryParameterConfiguration> GetConfigurationAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        var settings = await LoadSettingsAsync(vehicleId, IsAdsbParameter, cancellationToken).ConfigureAwait(false);
        return new MandatoryParameterConfiguration(settings,
        [
            "Configure only the transponder identity assigned to this aircraft; never copy another aircraft's ICAO address.",
            "Avoidance behavior depends on the reported AVD parameters and does not replace see-and-avoid responsibilities."
        ]);
    }

    /// <inheritdoc />
    public Task<MandatoryParameterApplyResult> ApplyAsync(VehicleId vehicleId, string name, double value, CancellationToken cancellationToken = default)
    {
        return base.ApplyAsync(vehicleId, name, value, Validate, cancellationToken);
    }

    private static bool IsAdsbParameter(string name)
    {
        return name.StartsWith("ADSB_", StringComparison.Ordinal) || name.StartsWith("AVD_", StringComparison.Ordinal);
    }

    private static string? Validate(string name, double value)
    {
        if (!double.IsFinite(value))
        {
            return $"{name} must be a finite value.";
        }

        if (name == "ADSB_ICAO_ID" && value is < 0 or > 16777215)
        {
            return "ADSB_ICAO_ID must be a valid 24-bit ICAO address (0 to 16777215).";
        }

        return null;
    }
}
