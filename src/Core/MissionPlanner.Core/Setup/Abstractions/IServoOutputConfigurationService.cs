using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Abstractions;

/// <summary>Projects servo output functions with live PWM and applies confirmed function writes.</summary>
public interface IServoOutputConfigurationService
{
    /// <summary>Builds the servo output configuration for the active vehicle.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="cancellationToken">A token that cancels metadata resolution.</param>
    /// <returns>The servo output configuration projection.</returns>
    Task<ServoOutputConfiguration> GetConfigurationAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>Writes and confirms modified settings for one physical servo output.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="settings">The desired settings for the physical output.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or failed apply result.</returns>
    Task<ServoOutputApplyResult> SetOutputAsync(VehicleId vehicleId, ServoOutputSettings settings, CancellationToken cancellationToken = default);

    /// <summary>Writes and confirms only the function assigned to one servo output.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="output">The one-based physical output number.</param>
    /// <param name="functionValue">The function value to assign.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The confirmed or failed apply result.</returns>
    Task<ServoOutputApplyResult> SetFunctionAsync(VehicleId vehicleId, int output, int functionValue, CancellationToken cancellationToken = default);
}
