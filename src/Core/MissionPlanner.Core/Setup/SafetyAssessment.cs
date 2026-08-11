using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Represents the immutable safety assessment projected by the Setup UI.</summary>
/// <param name="VehicleId">The assessed vehicle.</param>
/// <param name="Items">The assessed safety checks.</param>
/// <param name="Warnings">The evidence-based contradiction or gap warnings.</param>
public sealed record SafetyAssessment(
    VehicleId VehicleId,
    IReadOnlyList<SafetyCheckItem> Items,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Creates an empty assessment for the specified vehicle.</summary>
    /// <param name="vehicleId">The vehicle identifier.</param>
    /// <returns>An empty assessment.</returns>
    public static SafetyAssessment Empty(VehicleId vehicleId)
    {
        return new SafetyAssessment(vehicleId, [], []);
    }
}
