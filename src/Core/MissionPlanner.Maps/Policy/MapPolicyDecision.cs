namespace MissionPlanner.Maps.Policy;

/// <summary>Describes the effective decision for one map operation.</summary>
/// <param name="Operation">Evaluated operation.</param>
/// <param name="IsAllowed">Whether the operation is allowed.</param>
/// <param name="PolicyId">Policy that produced the decision.</param>
/// <param name="Reason">Human-readable decision reason.</param>
public sealed record MapPolicyDecision(MapOperation Operation, bool IsAllowed, string PolicyId, string Reason);
