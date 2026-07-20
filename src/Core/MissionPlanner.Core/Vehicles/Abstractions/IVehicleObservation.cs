namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>
/// Provides the public API for IVehicleObservation.
/// </summary>
public interface IVehicleObservation
{
    /// <summary>
    /// Provides the public API for ObservedAt.
    /// </summary>
    DateTimeOffset ObservedAt { get; }
}
