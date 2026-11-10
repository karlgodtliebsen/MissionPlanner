using System.Globalization;

namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Formats parameter values without exposing REAL32-to-double expansion noise.</summary>
public static class ParameterValueText
{
    /// <summary>Uses the shortest lossless representation, preserving explicit double edits and integers.</summary>
    public static string Format(double value, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.InvariantCulture;
        return double.IsFinite(value) && value == (double)(float)value
            ? ((float)value).ToString("R", provider)
            : value.ToString("R", provider);
    }

    /// <summary>Displays a difference using the same decimal operands shown to the user.</summary>
    public static string FormatDifference(double left, double right, IFormatProvider? provider = null)
    {
        provider ??= CultureInfo.InvariantCulture;
        if (decimal.TryParse(Format(left), NumberStyles.Float, CultureInfo.InvariantCulture, out var first) &&
            decimal.TryParse(Format(right), NumberStyles.Float, CultureInfo.InvariantCulture, out var second) &&
            (first != 0 || left == 0) && (second != 0 || right == 0))
        {
            try { return (second - first).ToString("G29", provider); }
            catch (OverflowException) { }
        }
        return (right - left).ToString("G9", provider);
    }
}
