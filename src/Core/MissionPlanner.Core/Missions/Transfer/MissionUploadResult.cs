namespace MissionPlanner.Core.Missions.Transfer;

/// <summary>
/// Provides the public API for MissionUploadResult.
/// </summary>
public sealed record MissionUploadResult(bool Success, byte? AckResult, string? Error);
