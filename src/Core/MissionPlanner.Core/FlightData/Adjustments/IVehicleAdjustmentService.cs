using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Adjustments;

/// <summary>Changes the speed, altitude and loiter radius of a vehicle that is flying.</summary>
public interface IVehicleAdjustmentService
{
    /// <summary>Evaluates whether a speed change is currently allowed.</summary>
    VehicleCommandDecision EvaluateSpeed(VehicleState state, VehicleSpeedTargetType targetType);
    /// <summary>Evaluates whether a guided altitude change is currently allowed.</summary>
    VehicleCommandDecision EvaluateAltitude(VehicleState state);
    /// <summary>Evaluates whether a loiter radius change is currently allowed.</summary>
    VehicleCommandDecision EvaluateLoiterRadius(VehicleState state);
    /// <summary>Sends a new speed target and reports how far it was confirmed.</summary>
    Task<VehicleAdjustmentResult> ChangeSpeedAsync(VehicleId vehicleId, VehicleSpeedTargetType targetType, double metersPerSecond, CancellationToken cancellationToken);
    /// <summary>Sends a new guided altitude relative to home and reports how far it was confirmed.</summary>
    Task<VehicleAdjustmentResult> SetGuidedAltitudeAsync(VehicleId vehicleId, double homeRelativeMeters, CancellationToken cancellationToken);
    /// <summary>Sets the loiter radius and reports how far it was confirmed.</summary>
    Task<VehicleAdjustmentResult> SetLoiterRadiusAsync(VehicleId vehicleId, double magnitudeMeters, CancellationToken cancellationToken);
}
