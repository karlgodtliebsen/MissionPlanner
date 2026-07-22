namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Contains vehicle vibration and accelerometer clipping diagnostics.</summary>
/// <param name="X">The X-axis vibration metric.</param>
/// <param name="Y">The Y-axis vibration metric.</param>
/// <param name="Z">The Z-axis vibration metric.</param>
/// <param name="Clipping">Per-IMU clipping counters.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleVibrationState(double? X, double? Y, double? Z, IReadOnlyList<uint> Clipping, DateTimeOffset? ObservedAt)
{
    /// <summary>Gets empty vibration state.</summary>
    public static VehicleVibrationState Empty { get; } = new(null, null, null, Array.Empty<uint>(), null);

    /// <summary>Returns whether vibration telemetry is stale.</summary>
    public bool IsStale(DateTimeOffset now, TimeSpan maximumAge) => ObservedAt is null || now - ObservedAt > maximumAge;
}
