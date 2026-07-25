namespace MissionPlanner.Library.Math;

public static class MathUtils
{
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
            decimalMinimum +
            decimal.Round(
                (decimalLeft - decimalMinimum) / decimalStep,
                0,
                MidpointRounding.AwayFromZero) *
            decimalStep;

        var normalizedRight =
            decimalMinimum +
            decimal.Round(
                (decimalRight - decimalMinimum) / decimalStep,
                0,
                MidpointRounding.AwayFromZero) *
            decimalStep;

        return normalizedLeft == normalizedRight;
    }
}
