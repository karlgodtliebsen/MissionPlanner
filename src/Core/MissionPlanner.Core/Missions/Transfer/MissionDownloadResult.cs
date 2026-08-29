using MissionPlanner.MavLink.Missions;

namespace MissionPlanner.Core.Missions.Transfer;

/// <summary>
/// Provides the public API for MissionDownloadResult.
/// </summary>
public sealed record MissionDownloadResult(
    bool Success,
    IReadOnlyList<MavLinkMissionItem> Items,
    string? Error,
    MissionPlanner.Shared.Models.Vehicles.Models.VehicleId? VehicleId = null,
    MissionPlanner.Core.Missions.Models.MissionPlanType? MissionType = null,
    uint? MissionId = null,
    DateTimeOffset? RetrievedAt = null);
