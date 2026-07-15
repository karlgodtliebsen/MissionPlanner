namespace MissionPlanner.Core.Missions.Transfer;

public sealed record MissionUploadResult(bool Success, byte? AckResult, string? Error);
