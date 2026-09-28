namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Describes one of the six firmware flight-mode PWM slots.</summary>
/// <param name="Slot">The one-based slot number.</param>
/// <param name="PwmLow">The inclusive lower PWM bound for the slot.</param>
/// <param name="PwmHigh">The inclusive upper PWM bound for the slot.</param>
/// <param name="SelectedModeNumber">The exact received parameter value, or null when the parameter has not loaded.</param>
/// <param name="SelectedModeName">The configured mode name, or a numeric fallback.</param>
/// <param name="IsActive">Whether the mode channel currently selects this slot.</param>
public sealed record FlightModeSlot(
    int Slot,
    int PwmLow,
    int PwmHigh,
    float? SelectedModeNumber,
    string SelectedModeName,
    bool IsActive);
