using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Projects servo output functions with live PWM and applies confirmed function writes.</summary>
public interface IServoOutputConfigurationService
{
    /// <summary>Builds the servo output configuration for the active vehicle.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="cancellationToken">A token that cancels metadata resolution.</param>
    /// <returns>The servo output configuration projection.</returns>
    Task<ServoOutputConfiguration> GetConfigurationAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Writes and confirms the function assigned to one servo output.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="output">The one-based servo output number.</param>
    /// <param name="functionValue">The function value to assign.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or failed apply result.</returns>
    Task<ServoOutputApplyResult> SetFunctionAsync(VehicleId vehicleId, int output, int functionValue, CancellationToken cancellationToken = default);
}
