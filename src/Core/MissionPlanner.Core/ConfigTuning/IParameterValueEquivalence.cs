namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Compares parameter values using firmware precision when it is available.</summary>
public interface IParameterValueEquivalence
{
    /// <summary>Returns whether two values represent the same parameter value.</summary>
    bool AreEquivalent(double left, double right, ParameterFieldMetadata? metadata = null);
}
