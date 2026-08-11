namespace MissionPlanner.Core.FlightData.Auxiliary;

/// <summary>Classifies the operator risk of an auxiliary function.</summary>
public enum AuxiliaryFunctionHazard
{
    /// <summary>No additional confirmation is required.</summary>
    Safe,

    /// <summary>The operator must explicitly confirm the action.</summary>
    Warning,

    /// <summary>The action is safety-critical and may be unavailable generically.</summary>
    High
}
