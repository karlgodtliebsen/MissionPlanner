using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Describes one of the six firmware flight-mode PWM slots.</summary>
/// <param name="Slot">The one-based slot number.</param>
/// <param name="PwmLow">The inclusive lower PWM bound for the slot.</param>
/// <param name="PwmHigh">The inclusive upper PWM bound for the slot.</param>
/// <param name="SelectedModeNumber">The configured firmware custom-mode number.</param>
/// <param name="SelectedModeName">The configured mode name, or a numeric fallback.</param>
/// <param name="IsActive">Whether the mode channel currently selects this slot.</param>
public sealed record FlightModeSlot(
    int Slot,
    int PwmLow,
    int PwmHigh,
    int SelectedModeNumber,
    string SelectedModeName,
    bool IsActive);

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
    public static FlightModeConfiguration Unsupported(VehicleId vehicleId, FirmwareFamily family) =>
        new(vehicleId, family, false, 0, [], [], null);
}

/// <summary>Represents the outcome of a confirmed flight-mode slot write.</summary>
/// <param name="Success">Whether the vehicle confirmed the new mode by readback.</param>
/// <param name="Message">A user-facing explanation of the outcome.</param>
public sealed record FlightModeApplyResult(bool Success, string Message);
