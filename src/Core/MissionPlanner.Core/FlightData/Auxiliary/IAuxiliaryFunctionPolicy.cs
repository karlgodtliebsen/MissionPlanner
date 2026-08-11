namespace MissionPlanner.Core.FlightData.Auxiliary;

/// <summary>Evaluates whether an auxiliary function can use the generic workflow.</summary>
public interface IAuxiliaryFunctionPolicy
{
    /// <summary>Returns a denial reason, or <see langword="null"/> when execution is allowed.</summary>
    string? GetDenialReason(AuxiliaryFunctionRequest request);
}
