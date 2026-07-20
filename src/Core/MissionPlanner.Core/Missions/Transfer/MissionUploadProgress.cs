namespace MissionPlanner.Core.Missions.Transfer;

/// <summary>
/// Provides the public API for MissionUploadProgress.
/// </summary>
public sealed record MissionUploadProgress(int SentItems, int TotalItems, ushort? RequestedSequence);
