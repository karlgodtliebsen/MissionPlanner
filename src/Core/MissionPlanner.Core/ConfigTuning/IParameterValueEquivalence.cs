namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Compares values at MAVLink parameter wire precision without quantizing to editor steps.</summary>
public interface IParameterValueEquivalence
{
    /// <summary>Returns whether two values represent the same parameter value.</summary>
    bool AreEquivalent(double left, double right, ParameterFieldMetadata? metadata = null);
}
