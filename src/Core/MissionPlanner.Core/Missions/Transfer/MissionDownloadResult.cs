using MissionPlanner.MavLink.Missions;

namespace MissionPlanner.Core.Missions.Transfer;

public sealed record MissionDownloadResult(bool Success, IReadOnlyList<MavLinkMissionItem> Items, string? Error);
