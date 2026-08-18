using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Represents the immutable battery configuration projected by the Setup UI.</summary>
/// <param name="VehicleId">The vehicle the configuration belongs to.</param>
/// <param name="Instances">The discovered battery instances in ascending order.</param>
/// <param name="MonitorOptions">The available monitor backends from metadata.</param>
/// <param name="LowActionOptions">The available low-failsafe actions from metadata.</param>
/// <param name="CriticalActionOptions">The available critical-failsafe actions from metadata.</param>
/// <param name="Issues">The detected configuration issues.</param>
public sealed record BatteryConfiguration(
    VehicleId VehicleId,
    IReadOnlyList<BatteryMonitorInstance> Instances,
    IReadOnlyList<BatterySettingOption> MonitorOptions,
    IReadOnlyList<BatterySettingOption> LowActionOptions,
    IReadOnlyList<BatterySettingOption> CriticalActionOptions,
    IReadOnlyList<BatteryValidationIssue> Issues)
{
    /// <summary>Creates an empty configuration for the specified vehicle.</summary>
    /// <param name="vehicleId">The vehicle identifier.</param>
    /// <returns>An empty configuration.</returns>
    public static BatteryConfiguration Empty(VehicleId vehicleId)
    {
        return new BatteryConfiguration(vehicleId, [], [], [], [], []);
    }
}
