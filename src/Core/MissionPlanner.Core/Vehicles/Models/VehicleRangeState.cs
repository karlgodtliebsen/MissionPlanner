namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Contains one range-sensor observation.</summary>
/// <param name="Id">The sensor identifier.</param>
/// <param name="DistanceMeters">The measured distance in metres, or <see langword="null"/> when invalid.</param>
/// <param name="MinimumMeters">The minimum measurable range in metres.</param>
/// <param name="MaximumMeters">The maximum measurable range in metres.</param>
/// <param name="Orientation">The MAV_SENSOR_ORIENTATION value.</param>
/// <param name="SignalQualityPercent">The signal quality percentage, or <see langword="null"/> when unknown.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleRangeSensorState(byte Id, double? DistanceMeters, double MinimumMeters, double MaximumMeters, byte Orientation, int? SignalQualityPercent, DateTimeOffset ObservedAt);

/// <summary>Contains latest range observations keyed by sensor ID.</summary>
/// <param name="Sensors">The keyed sensor samples.</param>
public sealed record VehicleRangeState(IReadOnlyDictionary<byte, VehicleRangeSensorState> Sensors)
{
    /// <summary>Gets empty range state.</summary>
    public static VehicleRangeState Empty { get; } = new(new Dictionary<byte, VehicleRangeSensorState>());
}
