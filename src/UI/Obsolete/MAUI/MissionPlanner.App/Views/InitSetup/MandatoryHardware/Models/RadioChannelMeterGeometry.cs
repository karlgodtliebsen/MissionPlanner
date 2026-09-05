namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models;

/// <summary>Provides allocation-free PWM-to-rail geometry for radio channel meters.</summary>
public static class RadioChannelMeterGeometry
{
    /// <summary>Maps a PWM value to a clamped horizontal position.</summary>
    public static float Position(int pwm, int displayMinimum, int displayMaximum, float left, float width)
    {
        if (displayMaximum <= displayMinimum || width <= 0)
        {
            return left;
        }

        var ratio = Math.Clamp((pwm - displayMinimum) / (double)(displayMaximum - displayMinimum), 0, 1);
        return left + (float)(ratio * width);
    }

    /// <summary>Returns the clamped dead-zone rail positions around the supplied trim.</summary>
    public static (float Left, float Right) DeadZone(
        int trim,
        int deadZone,
        int displayMinimum,
        int displayMaximum,
        float left,
        float width)
    {
        var radius = Math.Max(0, deadZone);
        return (
            Position(trim - radius, displayMinimum, displayMaximum, left, width),
            Position(trim + radius, displayMinimum, displayMaximum, left, width));
    }
}
