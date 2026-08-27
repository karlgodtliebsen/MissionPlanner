namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>Converts motor-spin values between UI percentages and ArduPilot normalized fractions.</summary>
public static class MotorSpinPercentage
{
    /// <summary>Converts a percentage to a normalized fraction.</summary>
    /// <param name="percent">The percentage value.</param>
    /// <returns>The normalized value.</returns>
    public static float ToNormalized(double percent) => (float)(percent / 100d);

    /// <summary>Converts a normalized fraction to a percentage.</summary>
    /// <param name="normalized">The normalized value.</param>
    /// <returns>The percentage value.</returns>
    public static double ToPercent(float normalized) => normalized * 100d;

    /// <summary>Converts a normalized motor-spin value to its whole percentage-point representation.</summary>
    /// <param name="normalized">The normalized value.</param>
    /// <returns>The rounded whole percentage.</returns>
    public static int ToWholePercent(float normalized) =>
        checked((int)Math.Round(ToPercent(normalized), MidpointRounding.AwayFromZero));
}
