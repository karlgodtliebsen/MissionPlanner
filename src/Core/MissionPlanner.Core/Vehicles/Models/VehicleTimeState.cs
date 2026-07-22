namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Contains the latest vehicle system-clock observation.</summary>
/// <param name="UnixTime">The vehicle UTC time, or <see langword="null"/> when unavailable.</param>
/// <param name="BootTime">The elapsed time since boot.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleTimeState(DateTimeOffset? UnixTime, TimeSpan BootTime, DateTimeOffset? ObservedAt)
{
    /// <summary>Gets empty time state.</summary>
    public static VehicleTimeState Empty { get; } = new(null, TimeSpan.Zero, null);

    /// <summary>Returns whether clock telemetry is stale.</summary>
    public bool IsStale(DateTimeOffset now, TimeSpan maximumAge) => ObservedAt is null || now - ObservedAt > maximumAge;
}
