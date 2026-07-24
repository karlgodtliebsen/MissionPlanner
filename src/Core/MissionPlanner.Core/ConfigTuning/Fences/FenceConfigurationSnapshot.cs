using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning.Fences;

/// <summary>Projects local, synchronized, and recoverable fence revisions.</summary>
/// <param name="VehicleId">The workspace vehicle.</param>
/// <param name="LocalPlan">The editable local plan.</param>
/// <param name="VehiclePlan">The last plan confirmed from or to the vehicle.</param>
/// <param name="BackupPlan">The plan saved before the latest replace or clear.</param>
/// <param name="LocalRevision">The monotonic local revision.</param>
/// <param name="VehicleRevision">The monotonic synchronized revision.</param>
/// <param name="IsDirty">Whether local geometry differs from the synchronized revision.</param>
public sealed record FenceConfigurationSnapshot(
    VehicleId VehicleId,
    FencePlan LocalPlan,
    FencePlan? VehiclePlan,
    FencePlan? BackupPlan,
    long LocalRevision,
    long? VehicleRevision,
    bool IsDirty);
