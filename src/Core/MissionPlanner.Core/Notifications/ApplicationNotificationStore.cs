using Microsoft.Extensions.Options;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Notifications;

/// <summary>
/// Provides a thread-safe bounded local-notification history.
/// </summary>
public sealed class ApplicationNotificationStore : IApplicationNotificationStore
{
    private readonly object sync = new();
    private readonly List<ApplicationNotificationEntry> entries = [];
    private readonly int capacity;
    private long nextIdentity;

    /// <summary>Initializes a local-notification store.</summary>
    /// <param name="options">The shared message-history options.</param>
    public ApplicationNotificationStore(IOptions<VehicleMessageStoreOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        capacity = Math.Max(1, options.Value.Capacity);
    }

    /// <inheritdoc />
    public event EventHandler<ApplicationNotificationAddedEventArgs>? NotificationAdded;

    /// <inheritdoc />
    public void Add(UserNotification notification, DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var entry = new ApplicationNotificationEntry(
            Interlocked.Increment(ref nextIdentity),
            notification.VehicleId,
            notification.Title,
            notification.Message,
            notification.Severity,
            receivedAt);
        lock (sync)
        {
            entries.Add(entry);
            if (entries.Count > capacity)
            {
                entries.RemoveRange(0, entries.Count - capacity);
            }
        }

        NotificationAdded?.Invoke(this, new ApplicationNotificationAddedEventArgs(entry));
    }

    /// <inheritdoc />
    public IReadOnlyList<ApplicationNotificationEntry> GetMessages(VehicleId vehicleId)
    {
        lock (sync)
        {
            return entries.Where(entry => entry.VehicleId is null || entry.VehicleId == vehicleId).ToArray();
        }
    }

    /// <inheritdoc />
    public void Clear(VehicleId vehicleId, Func<ApplicationNotificationEntry, bool>? predicate = null)
    {
        lock (sync)
        {
            entries.RemoveAll(entry =>
                (entry.VehicleId is null || entry.VehicleId == vehicleId) &&
                (predicate is null || predicate(entry)));
        }
    }
}
