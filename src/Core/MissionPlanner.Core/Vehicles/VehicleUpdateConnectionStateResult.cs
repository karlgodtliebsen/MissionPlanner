namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Represents the result of updating the connection state of vehicles.
/// </summary>
/// <param name="Vehicles">The collection of vehicles with updated connection states.</param>
public sealed record VehicleUpdateConnectionStateResult(IReadOnlyCollection<VehicleSession> Vehicles);
