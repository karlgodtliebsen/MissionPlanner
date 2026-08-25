using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Missions.Rally;

/// <summary>Active-vehicle rally workspace state.</summary>
public sealed record RallyPlanSnapshot(VehicleId VehicleId, RallyPlan LocalPlan, RallyPlan? VehiclePlan,
    long LocalRevision, long? VehicleRevision, bool IsDirty, DateTimeOffset? LastDownloadedAt);