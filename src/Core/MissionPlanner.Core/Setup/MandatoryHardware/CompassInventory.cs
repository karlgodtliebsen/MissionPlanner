using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Represents the immutable compass inventory projected by the Setup UI.</summary>
/// <param name="VehicleId">The vehicle the inventory belongs to.</param>
/// <param name="Compasses">The discovered compass instances in slot order.</param>
/// <param name="OrientationOptions">The orientation choices available for editing.</param>
/// <param name="Issues">The detected duplicate-identity or priority inconsistencies.</param>
public sealed record CompassInventory(
    VehicleId VehicleId,
    IReadOnlyList<CompassInstance> Compasses,
    IReadOnlyList<CompassOrientationOption> OrientationOptions,
    IReadOnlyList<CompassConfigurationIssue> Issues)
{
    /// <summary>Gets an empty inventory for the specified vehicle.</summary>
    /// <param name="vehicleId">The vehicle identifier.</param>
    /// <returns>An inventory with no compasses.</returns>
    public static CompassInventory Empty(VehicleId vehicleId)
    {
        return new CompassInventory(vehicleId, [], [], []);
    }
}
