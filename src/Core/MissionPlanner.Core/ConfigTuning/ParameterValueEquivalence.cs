namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Applies one comparison policy to edited, imported, compared and read-back values.</summary>
public sealed class ParameterValueEquivalence : IParameterValueEquivalence
{
    /// <summary>The default stateless comparer.</summary>
    public static ParameterValueEquivalence Default { get; } = new();

    /// <inheritdoc />
    public bool AreEquivalent(double left, double right, ParameterFieldMetadata? metadata = null)
    {
        if (double.IsNaN(left) || double.IsNaN(right))
        {
            return double.IsNaN(left) && double.IsNaN(right);
        }

        if (double.IsInfinity(left) || double.IsInfinity(right))
        {
            return left.Equals(right);
        }

        const double absoluteTolerance = 1e-6;
        const double relativeTolerance = 1e-5;
        var tolerance = Math.Max(
            absoluteTolerance,
            relativeTolerance * Math.Max(Math.Abs(left), Math.Abs(right)));

        // Absorb float32 wire expansion without treating adjacent steps as equal.
        if (metadata?.Increment is > 0 and var increment)
        {
            tolerance = Math.Max(tolerance, increment * 1e-4);
        }

        return Math.Abs(left - right) <= tolerance;
    }
}
