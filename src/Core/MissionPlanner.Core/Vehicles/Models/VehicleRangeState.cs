namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Contains latest range observations keyed by sensor ID.</summary>
/// <param name="Sensors">The keyed sensor samples.</param>
public sealed record VehicleRangeState(IReadOnlyDictionary<byte, VehicleRangeSensorState> Sensors)
{
    /// <summary>Gets empty range state.</summary>
    public static VehicleRangeState Empty { get; } = new(new Dictionary<byte, VehicleRangeSensorState>());
}
