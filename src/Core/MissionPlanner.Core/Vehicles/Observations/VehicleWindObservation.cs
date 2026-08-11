using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>Represents a normalized wind vector.</summary>
/// <param name="NorthMetersPerSecond">The north component.</param>
/// <param name="EastMetersPerSecond">The east component.</param>
/// <param name="DownMetersPerSecond">The down component.</param>
/// <param name="HorizontalVariance">The horizontal variance.</param>
/// <param name="VerticalVariance">The vertical variance.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleWindObservation(double? NorthMetersPerSecond, double? EastMetersPerSecond, double? DownMetersPerSecond, double? HorizontalVariance, double? VerticalVariance, DateTimeOffset ObservedAt) : IVehicleObservation;
