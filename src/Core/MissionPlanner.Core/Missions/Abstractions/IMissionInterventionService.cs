using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Missions.Abstractions;

/// <summary>Executes safety-gated mission state-machine interventions for one explicit vehicle.</summary>
public interface IMissionInterventionService
{
    Task<MissionInterventionResult> SetCurrentMissionItemAsync(VehicleId vehicleId, ushort sequence, CancellationToken cancellationToken);
    Task<MissionInterventionResult> RestartMissionAsync(VehicleId vehicleId, CancellationToken cancellationToken);
    Task<MissionInterventionResult> ResumeMissionAsync(VehicleId vehicleId, CancellationToken cancellationToken);
    Task<MissionInterventionResult> AbortLandingAsync(VehicleId vehicleId, CancellationToken cancellationToken);
}
