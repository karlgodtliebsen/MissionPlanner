using MissionPlanner.MavLink.Missions;

namespace MissionPlanner.Core.Missions.Transfer;

/// <summary>
/// Provides the public API for MissionDownloadResult.
/// </summary>
public sealed record MissionDownloadResult(bool Success, IReadOnlyList<MavLinkMissionItem> Items, string? Error);
