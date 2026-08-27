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
    /// <param name="marginPercent">The positive percentage-point margin above motor-test throttle.</param>
    /// <returns>The recommendation or validation failure.</returns>
    MotorSpinRecommendation RecommendSpinArm(VehicleId vehicleId, double testThrottlePercent, double marginPercent = 2);

    /// <summary>Calculates the recommended MOT_SPIN_MIN value from MOT_SPIN_ARM.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="marginPercent">The positive percentage-point margin above the current MOT_SPIN_ARM.</param>
    /// <returns>The recommendation or validation failure.</returns>
    MotorSpinRecommendation RecommendSpinMin(VehicleId vehicleId, double marginPercent = 3);

    /// <summary>Validates, writes, and confirms MOT_SPIN_ARM.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="testThrottlePercent">The selected motor-test throttle percentage.</param>
    /// <param name="marginPercent">The positive percentage-point margin above motor-test throttle.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or failed operation result.</returns>
    Task<MotorSpinWriteResult> SetSpinArmAsync(VehicleId vehicleId, double testThrottlePercent, double marginPercent = 2, CancellationToken cancellationToken = default);

    /// <summary>Validates, writes, and confirms MOT_SPIN_MIN.</summary>
    /// <param name="vehicleId">The target vehicle.</param>
    /// <param name="marginPercent">The positive percentage-point margin above the current MOT_SPIN_ARM.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or failed operation result.</returns>
    Task<MotorSpinWriteResult> SetSpinMinAsync(VehicleId vehicleId, double marginPercent = 3, CancellationToken cancellationToken = default);
}
