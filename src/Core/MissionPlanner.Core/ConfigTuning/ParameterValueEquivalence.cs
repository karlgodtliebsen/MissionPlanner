namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Applies one comparison policy to edited, imported, compared and read-back values.</summary>
public sealed class ParameterValueEquivalence : IParameterValueEquivalence
{
    /// <summary>The default stateless comparer.</summary>
    public static ParameterValueEquivalence Default { get; } = new();

    /// <inheritdoc />
    public bool AreEquivalent(double left, double right, ParameterFieldMetadata? metadata = null)
    {
        // Increment is an editor stepping hint, not the precision of a stored value.
        // Compare the value sent over PARAM_SET, preserving smaller calibration changes.
        return double.IsFinite(left) && double.IsFinite(right) &&
            (left == right || float.IsFinite((float)left) && (float)left == (float)right);
    }
}
