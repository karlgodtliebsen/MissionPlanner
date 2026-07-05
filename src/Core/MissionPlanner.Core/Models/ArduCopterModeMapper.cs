namespace MissionPlanner.Core.Models;

/// <summary>
/// Provides mapping between <see cref="VehicleMode"/> and ArduCopter custom mode values.
/// </summary>
public static class ArduCopterModeMapper
{
    /// <summary>
    /// Converts a <see cref="VehicleMode"/> to its corresponding ArduCopter custom mode value.
    /// </summary>
    /// <param name="mode">The vehicle mode to convert.</param>
    /// <returns>The corresponding ArduCopter custom mode value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the vehicle mode is not supported.</exception>
    public static uint ToCustomMode(VehicleMode mode)
    {
        return mode switch
        {
            VehicleMode.Stabilize => 0,
            VehicleMode.AltHold => 2,
            VehicleMode.Guided => 4,
            VehicleMode.Loiter => 5,
            VehicleMode.Rtl => 6,
            VehicleMode.Land => 9,
            var _ => throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "Unsupported ArduCopter vehicle mode.")
        };
    }

    /// <summary>
    /// Converts an ArduCopter custom mode value to its corresponding <see cref="VehicleMode"/>.
    /// </summary>
    /// <param name="customMode">The ArduCopter custom mode value to convert.</param>
    /// <returns>The corresponding <see cref="VehicleMode"/>.</returns>
    public static VehicleMode ToVehicleMode(uint customMode)
    {
        return customMode switch
        {
            0 => VehicleMode.Stabilize,
            2 => VehicleMode.AltHold,
            4 => VehicleMode.Guided,
            5 => VehicleMode.Loiter,
            6 => VehicleMode.Rtl,
            9 => VehicleMode.Land,
            var _ => VehicleMode.Unknown
        };
    }
}