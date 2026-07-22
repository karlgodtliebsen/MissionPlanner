namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Represents the immutable application-facing snapshot of the currently selected vehicle.
/// </summary>
/// <param name="VehicleId">The selected vehicle identifier, or <see langword="null"/> when none is selected.</param>
/// <param name="State">The latest immutable vehicle state, or <see langword="null"/> when none has been observed.</param>
public sealed record ActiveVehicleSnapshot(VehicleId? VehicleId, VehicleState? State)
{
    /// <summary>
    /// Gets an empty snapshot used before a vehicle has been selected.
    /// </summary>
    public static ActiveVehicleSnapshot Empty { get; } = new(null, null);

    /// <summary>
    /// Gets a value indicating whether the active vehicle is online.
    /// </summary>
    public bool IsOnline => State?.ConnectionState == VehicleConnectionState.Online;

    /// <summary>
    /// Gets the active vehicle display name.
    /// </summary>
    public string DisplayName => State?.DisplayName ?? "No vehicle";
}
