using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Represents the immutable servo output configuration projected by the Setup UI.</summary>
/// <param name="VehicleId">The vehicle the configuration belongs to.</param>
/// <param name="Outputs">The discovered servo outputs in ascending order.</param>
/// <param name="FunctionOptions">The available function assignments from metadata.</param>
public sealed record ServoOutputConfiguration(
    VehicleId VehicleId,
    IReadOnlyList<ServoOutputInfo> Outputs,
    IReadOnlyList<ServoFunctionOption> FunctionOptions)
{
    /// <summary>Creates an empty configuration for the specified vehicle.</summary>
    /// <param name="vehicleId">The vehicle identifier.</param>
    /// <returns>An empty configuration.</returns>
    public static ServoOutputConfiguration Empty(VehicleId vehicleId)
    {
        return new ServoOutputConfiguration(vehicleId, [], []);
    }
}
