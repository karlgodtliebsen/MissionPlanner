using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Arming;

/// <summary>Semantic arming configuration over the existing registry and verified parameter editor.</summary>
public interface IArmingConfigurationService
{
    /// <summary>Projects cached parameters and firmware metadata without starting a parameter download.</summary>
    Task<ArmingSetupState> ReadAsync(VehicleId id, CancellationToken cancellationToken = default);
    /// <summary>Builds an explicit connection-scoped review without writing.</summary>
    Task<ArmingChangeSet> EvaluateChangesAsync(VehicleId id, ArmingConfiguration desired, CancellationToken cancellationToken = default);
    /// <summary>Revalidates and applies only reviewed changes, stopping at the first unverified write.</summary>
    Task<ArmingApplyResult> ApplyAsync(VehicleId id, ArmingChangeSet changes, CancellationToken cancellationToken = default);
}
