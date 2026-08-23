using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Abstractions;

/// <summary>Calculates and writes safe normalized motor-spin parameters.</summary>
public interface IMotorSpinParameterService
{
    /// <summary>Gets current motor-spin parameter availability and values.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <returns>The current registry-backed state.</returns>
    MotorSpinParameterState GetState(VehicleId vehicleId);

    /// <summary>Calculates the recommended MOT_SPIN_ARM value from a motor-test percentage.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="testThrottlePercent">The selected motor-test throttle percentage.</param>
    /// <returns>The recommendation or validation failure.</returns>
    MotorSpinRecommendation RecommendSpinArm(VehicleId vehicleId, double testThrottlePercent);

    /// <summary>Calculates the recommended MOT_SPIN_MIN value from MOT_SPIN_ARM.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <returns>The recommendation or validation failure.</returns>
    MotorSpinRecommendation RecommendSpinMin(VehicleId vehicleId);

    /// <summary>Validates, writes, and confirms MOT_SPIN_ARM.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="testThrottlePercent">The selected motor-test throttle percentage.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or failed operation result.</returns>
    Task<MotorSpinWriteResult> SetSpinArmAsync(VehicleId vehicleId, double testThrottlePercent, CancellationToken cancellationToken = default);

    /// <summary>Validates, writes, and confirms MOT_SPIN_MIN.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or failed operation result.</returns>
    Task<MotorSpinWriteResult> SetSpinMinAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);
}
