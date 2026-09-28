using MissionPlanner.Firmware;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Read-only local interpretation of raw receiver input; never an FC-reported command.</summary>
public sealed record RadioInputInterpretation(double? Normalized, string Description)
{
    /// <summary>Uses Copter centered-axis calibration and reversal, retaining missing values as unknown.</summary>
    public static RadioInputInterpretation Calculate(FirmwareFamily family, string? axis, int raw,
        float? minimum, float? trim, float? maximum, float? deadZone, float? reversed, bool fresh)
    {
        if (!fresh || raw is <= 0 or >= ushort.MaxValue)
        {
            return new(null, "Local interpretation unavailable: stale or missing RC input.");
        }
        if (family != FirmwareFamily.ArduCopter || axis is not ("Roll" or "Pitch" or "Yaw"))
        {
            return new(null, "Local direction unavailable for this firmware/axis; raw received input only.");
        }
        if (minimum is not { } min || trim is not { } center || maximum is not { } max ||
            deadZone is not { } dz || reversed is not (0 or 1) ||
            !float.IsFinite(min) || !float.IsFinite(center) || !float.IsFinite(max) || !float.IsFinite(dz) ||
            dz < 0 || min >= center - dz || max <= center + dz)
        {
            return new(null, "Local interpretation unavailable: missing/invalid MIN, TRIM, MAX, DZ or REVERSED.");
        }
        var amount = raw > center + dz ? (raw - center - dz) / (max - center - dz) :
            raw < center - dz ? (raw - center + dz) / (center - dz - min) : 0;
        amount = Math.Clamp(amount * (reversed == 1 ? -1 : 1), -1, 1);
        var direction = amount == 0 ? "Neutral (within dead zone)" : axis switch
        {
            "Pitch" => amount > 0 ? "Nose up" : "Nose down",
            "Roll" => amount > 0 ? "Roll right" : "Roll left",
            _ => amount > 0 ? "Yaw right" : "Yaw left"
        };
        return new(amount, $"Local interpretation: {direction} ({amount:P0}); not an FC-reported command.");
    }
}
