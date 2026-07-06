using MissionPlanner.Core.Models;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Services;

/// <summary>
/// Defines the contract for a vehicle registry, which manages the collection of vehicle sessions and handles heartbeat updates.
/// </summary>
public interface IVehicleRegistry
{
    /// <summary>
    /// Gets the vehicle session for the specified vehicle ID. Throws an exception if the vehicle is not found.
    /// </summary>
    /// <param name="vehicleId">The unique identifier of the vehicle.</param>
    /// <returns>The vehicle session associated with the specified vehicle ID.</returns>
    VehicleSession? GetRequired(VehicleId vehicleId);

    /// <summary>
    /// Gets the collection of registered vehicle sessions.
    /// </summary>
    IReadOnlyCollection<VehicleSession> Vehicles { get; }


    /// <summary>
    /// Resets the vehicle registry, clearing all registered vehicle sessions.
    /// </summary>
    void Reset();

    /// <summary>
    /// Updates the connection states of all registered vehicle sessions based on the elapsed time since their last heartbeat.
    /// </summary>
    /// <param name="now">The current date and time.</param>
    /// <param name="staleAfter">The time span after which a vehicle is considered stale.</param>
    /// <param name="degradedAfter">The time span after which a vehicle is considered degraded.</param>
    /// <param name="offlineAfter">The time span after which a vehicle is considered offline.</param>
    VehicleUpdateConnectionStateResult UpdateConnectionStates(DateTimeOffset now, TimeSpan staleAfter, TimeSpan degradedAfter, TimeSpan offlineAfter);

    /// <summary>
    /// Registers a new vehicle or updates an existing vehicle's state based on a received heartbeat message. 
    /// </summary>
    /// <param name="vehicleId">The unique identifier of the vehicle.</param>
    /// <param name="endPoint">The endpoint of the vehicle.</param>
    /// <param name="customMode">The custom mode of the vehicle.</param>
    /// <param name="vehicleType">The type of the vehicle.</param>
    /// <param name="autopilot">The autopilot type of the vehicle.</param>
    /// <param name="baseMode">The base mode of the vehicle.</param>
    /// <param name="systemStatus">The system status of the vehicle.</param>
    /// <param name="mavLinkVersion">The MAVLink version of the vehicle.</param>
    /// <param name="receivedAt">The timestamp when the heartbeat was received.</param>
    /// <returns>The updated or newly registered vehicle session.</returns>
    VehicleRegistryResult RegisterOrUpdateHeartbeat(
        VehicleId vehicleId,
        TransportEndPoint endPoint,
        uint customMode,
        byte vehicleType,
        byte autopilot,
        byte baseMode,
        byte systemStatus,
        byte mavLinkVersion,
        DateTimeOffset receivedAt);
}
