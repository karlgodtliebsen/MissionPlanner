namespace MissionPlanner.Core.Diagnostics;

/// <summary>Session-scoped FC battery blocker, retained until positively resolved or reconnected.</summary>
public sealed record BatteryArmingEvidence(int? BatteryNumber, string Message, DateTimeOffset ObservedAt, DateTimeOffset? ResolvedAt = null);
