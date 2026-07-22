using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>
/// Stores bounded, isolated status-text histories for connected vehicles.
/// </summary>
public interface IVehicleMessageStore
{
    /// <summary>Occurs when a message is appended.</summary>
    event EventHandler<VehicleStatusTextAddedEventArgs>? MessageAdded;

    /// <summary>Gets an immutable snapshot of a vehicle's history.</summary>
    /// <param name="vehicleId">The vehicle whose history is requested.</param>
    /// <returns>The messages in arrival order.</returns>
    IReadOnlyList<VehicleStatusText> GetMessages(VehicleId vehicleId);

    /// <summary>Adds a message and evicts the oldest item when capacity is exceeded.</summary>
    /// <param name="message">The message to append.</param>
    /// <returns>The stored message with its assigned identity.</returns>
    VehicleStatusText Add(VehicleStatusText message);

    /// <summary>Clears messages matching an optional predicate from one vehicle.</summary>
    /// <param name="vehicleId">The vehicle whose history is modified.</param>
    /// <param name="predicate">The optional predicate; when omitted, the complete history is cleared.</param>
    void Clear(VehicleId vehicleId, Func<VehicleStatusText, bool>? predicate = null);
}
