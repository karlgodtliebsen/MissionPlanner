namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>Platform-owned local storage of user descriptions keyed by hardware identity.</summary>
public interface IVehicleLocalDetailsStore
{
    /// <summary>Reads cached details without filesystem I/O on the telemetry path.</summary>
    VehicleLocalDetails? Get(string key);
    /// <summary>Saves details, returning a user-facing persistence status.</summary>
    string Save(string key, VehicleLocalDetails details);
}
