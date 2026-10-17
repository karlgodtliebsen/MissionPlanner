using System.Globalization;
using MissionPlanner.Library.Math;

namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Applies one comparison policy to edited, imported, compared and read-back values.</summary>
public sealed class ParameterValueEquivalence : IParameterValueEquivalence
{
    /// <summary>The default stateless comparer.</summary>
    public static ParameterValueEquivalence Default { get; } = new();

    /// <inheritdoc />
    public bool AreEquivalent(double left, double right, ParameterFieldMetadata? metadata = null)
    {
        // A decimal edit and its REAL32 readback can straddle an increment-rounding
        // boundary (for example 0.35 and 0.35f). Identical wire values are equal.
        if (double.IsFinite(left) && double.IsFinite(right) &&
            float.IsFinite((float)left) && (float)left == (float)right)
        {
            return true;
        }

        if (metadata?.Increment is > 0 and var increment)
        {
            return MathUtils.AreEquivalent(
                NormalizeWireValue(left),
                NormalizeWireValue(right),
                NormalizeWireValue(increment),
                NormalizeWireValue(metadata.Minimum ?? 0d));
        }

        return MathUtils.AreNearlyEqual(left, right, 1e-6, 1e-5);
    }

    private static double NormalizeWireValue(double value)
    {
        // Use the shortest decimal that round-trips to a received REAL32 before
        // rounding to metadata precision; its double expansion can change the bucket.
        return double.IsFinite(value) && value.Equals((double)(float)value)
            ? double.Parse(((float)value).ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            : value;
    }
}
