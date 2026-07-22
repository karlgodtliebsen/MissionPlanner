using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Notifications;

/// <summary>
/// Stores a bounded history of local application notifications independently of MAVLink messages.
/// </summary>
public interface IApplicationNotificationStore
{
    /// <summary>Occurs after a local notification is stored.</summary>
    event EventHandler<ApplicationNotificationAddedEventArgs>? NotificationAdded;

    /// <summary>Adds a timestamped notification.</summary>
    /// <param name="notification">The notification.</param>
    /// <param name="receivedAt">The recorded timestamp.</param>
    void Add(UserNotification notification, DateTimeOffset receivedAt);

    /// <summary>Gets notifications related to a vehicle plus application-wide notifications.</summary>
    /// <param name="vehicleId">The active vehicle.</param>
    /// <returns>The matching history in arrival order.</returns>
    IReadOnlyList<ApplicationNotificationEntry> GetMessages(VehicleId vehicleId);

    /// <summary>Clears matching notifications for a current vehicle view.</summary>
    /// <param name="vehicleId">The active vehicle.</param>
    /// <param name="predicate">An optional additional filter.</param>
    void Clear(VehicleId vehicleId, Func<ApplicationNotificationEntry, bool>? predicate = null);
}
