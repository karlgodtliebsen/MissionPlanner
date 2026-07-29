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
        if (metadata?.Increment is > 0 and var increment)
        {
            return MathUtils.AreEquivalent(
                left,
                right,
                increment,
                metadata.Minimum ?? 0d);
        }

        return MathUtils.AreNearlyEqual(left, right, 1e-6, 1e-5);
    }
}
