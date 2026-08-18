using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Represents the immutable flight-mode configuration projected by the Setup UI.</summary>
/// <param name="VehicleId">The vehicle the configuration belongs to.</param>
/// <param name="Family">The firmware family.</param>
/// <param name="IsSupported">Whether the connected firmware exposes a flight-mode channel.</param>
/// <param name="ModeChannel">The configured mode channel, or zero when unavailable.</param>
/// <param name="Slots">The six configured slots.</param>
/// <param name="Options">The modes available for the firmware family.</param>
/// <param name="ActiveSlot">The live active slot, or null when telemetry is stale or absent.</param>
public sealed record FlightModeConfiguration(
    VehicleId VehicleId,
    FirmwareFamily Family,
    bool IsSupported,
    int ModeChannel,
    IReadOnlyList<FlightModeSlot> Slots,
    IReadOnlyList<VehicleModeOption> Options,
    int? ActiveSlot)
{
    /// <summary>Creates an unsupported configuration for the specified vehicle and family.</summary>
    /// <param name="vehicleId">The vehicle identifier.</param>
    /// <param name="family">The firmware family.</param>
    /// <returns>An unsupported configuration.</returns>
    public static FlightModeConfiguration Unsupported(VehicleId vehicleId, FirmwareFamily family)
    {
        return new FlightModeConfiguration(vehicleId, family, false, 0, [], [], null);
    }
}
