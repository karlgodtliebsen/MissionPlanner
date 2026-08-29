using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Adjustments;

public interface IVehicleAdjustmentService
{
    VehicleCommandDecision EvaluateSpeed(VehicleState state, VehicleSpeedTargetType targetType);
    VehicleCommandDecision EvaluateAltitude(VehicleState state);
    VehicleCommandDecision EvaluateLoiterRadius(VehicleState state);
    Task<VehicleAdjustmentResult> ChangeSpeedAsync(VehicleId vehicleId, VehicleSpeedTargetType targetType, double metersPerSecond, CancellationToken cancellationToken);
    Task<VehicleAdjustmentResult> SetGuidedAltitudeAsync(VehicleId vehicleId, double homeRelativeMeters, CancellationToken cancellationToken);
    Task<VehicleAdjustmentResult> SetLoiterRadiusAsync(VehicleId vehicleId, double magnitudeMeters, CancellationToken cancellationToken);
}
