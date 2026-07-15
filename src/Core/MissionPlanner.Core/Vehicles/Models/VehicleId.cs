namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Represents the unique identifier of a vehicle, consisting of a system ID and a component ID.
/// </summary>
/// <param name="SystemId">The system ID of the vehicle.</param>
/// <param name="ComponentId">The component ID of the vehicle.</param>
public readonly record struct VehicleId(byte SystemId, byte ComponentId)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return $"{SystemId}:{ComponentId}";
    }
}
