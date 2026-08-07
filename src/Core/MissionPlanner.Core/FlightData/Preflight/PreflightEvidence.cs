namespace MissionPlanner.Core.FlightData.Preflight;

/// <summary>Provides the observation supporting a readiness result.</summary>
public sealed record PreflightEvidence(string Source, string Value, DateTimeOffset? ObservedAt);
