namespace MissionPlanner.Library.Math;

/// <summary>
/// Provides utility methods for mathematical operations.
/// </summary>
public static class MathUtils
{
    /// <summary>
    /// Determines whether two double-precision floating-point numbers are nearly equal,
    /// considering both absolute and relative tolerances.
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <param name="absoluteTolerance">The maximum allowed absolute difference.</param>
    /// <param name="relativeTolerance">The maximum allowed relative difference.</param>
    /// <returns></returns>
    public static bool AreNearlyEqual(double left, double right, double absoluteTolerance = 1e-9, double relativeTolerance = 1e-9)
    {
        if (left.Equals(right))
        {
            return true;
        }

        if (!double.IsFinite(left) || !double.IsFinite(right))
        {
            return false;
        }

        var difference = System.Math.Abs(left - right);

        if (difference <= absoluteTolerance)
        {
            return true;
        }

        var largestMagnitude =
            System.Math.Max(System.Math.Abs(left), System.Math.Abs(right));

        return difference <= largestMagnitude * relativeTolerance;
    }

    /// <summary>
    /// Determines whether two double-precision floating-point numbers are equivalent within a specified step size and minimum value.   
    /// </summary>
    /// <param name="left">The first value to compare.</param>
    /// <param name="right">The second value to compare.</param>
    /// <param name="stepSize">The step size for equivalence comparison.</param>
    /// <param name="minimum">The minimum value for normalization.</param>
    /// <returns>True if the values are equivalent within the specified step size and minimum value; otherwise, false.</returns>
    public static bool AreEquivalent(double left, double right, double stepSize, double minimum = 0d)
    {
        if (double.IsNaN(left) || double.IsNaN(right))
        {
            return double.IsNaN(left) && double.IsNaN(right);
        }

        if (double.IsInfinity(left) || double.IsInfinity(right))
        {
            return left.Equals(right);
        }

        if (!double.IsFinite(stepSize) || stepSize <= 0d)
        {
            return AreNearlyEqual(left, right);
        }

        var decimalLeft = (decimal)left;
        var decimalRight = (decimal)right;
        var decimalStep = System.Math.Abs((decimal)stepSize);
        var decimalMinimum = (decimal)minimum;

        var normalizedLeft =
            decimalMinimum + (decimal.Round((decimalLeft - decimalMinimum) / decimalStep, 0, MidpointRounding.AwayFromZero) * decimalStep);

        var normalizedRight =
            decimalMinimum + (decimal.Round((decimalRight - decimalMinimum) / decimalStep, 0, MidpointRounding.AwayFromZero) * decimalStep);

        return normalizedLeft == normalizedRight;
    }

    /// <summary>
    /// Formats a numeric value at the precision represented by its smallest meaningful step.
    /// </summary>
    public static string FormatAtStepPrecision(double value, double? stepSize, IFormatProvider? formatProvider = null)
    {
        formatProvider ??= System.Globalization.CultureInfo.CurrentCulture;

        if (!double.IsFinite(value))
        {
            return value.ToString(formatProvider);
        }

        if (stepSize is not > 0 || !double.IsFinite(stepSize.Value))
        {
            return value.ToString("G15", formatProvider);
        }

        decimal decimalStep;
        try
        {
            decimalStep = decimal.Abs((decimal)stepSize.Value);
        }
        catch (OverflowException)
        {
            return value.ToString("G15", formatProvider);
        }

        var scale = (decimal.GetBits(decimalStep)[3] >> 16) & 0x7f;
        var format = scale == 0
            ? "0"
            : $"0.{new string('#', scale)}";

        return value.ToString(format, formatProvider);
    }
}
