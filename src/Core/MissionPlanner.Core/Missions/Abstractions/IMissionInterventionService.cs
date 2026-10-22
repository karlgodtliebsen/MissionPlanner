using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Missions.Abstractions;

/// <summary>Executes safety-gated mission state-machine interventions for one explicit vehicle.</summary>
public interface IMissionInterventionService
{
    /// <summary>Evaluates whether a mission action is currently allowed.</summary>
    VehicleCommandDecision Evaluate(VehicleState state, VehicleAction action, ushort? sequence = null);
    /// <summary>Makes the given mission item the current one.</summary>
    Task<MissionInterventionResult> SetCurrentMissionItemAsync(VehicleId vehicleId, ushort sequence, CancellationToken cancellationToken);
    /// <summary>Restarts the mission from its first item.</summary>
    Task<MissionInterventionResult> RestartMissionAsync(VehicleId vehicleId, CancellationToken cancellationToken);
    /// <summary>Resumes a suspended mission.</summary>
    Task<MissionInterventionResult> ResumeMissionAsync(VehicleId vehicleId, CancellationToken cancellationToken);
    /// <summary>Aborts a landing in progress.</summary>
    Task<MissionInterventionResult> AbortLandingAsync(VehicleId vehicleId, CancellationToken cancellationToken);
}
