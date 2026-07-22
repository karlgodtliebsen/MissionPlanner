namespace MissionPlanner.Core.Notifications;

/// <summary>
/// Presents user notifications through a platform-specific UI adapter.
/// </summary>
public interface IUserNotificationService
{
    /// <summary>
    /// Presents the specified notification.
    /// </summary>
    /// <param name="notification">The notification to present.</param>
    /// <param name="cancellationToken">A token that cancels presentation.</param>
    /// <returns>A task that completes after the notification has been presented.</returns>
    Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken = default);
}
