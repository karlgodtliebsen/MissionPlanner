namespace MissionPlanner.Core.Notifications;

/// <summary>Carries a local application notification appended to history.</summary>
/// <param name="notification">The stored notification.</param>
public sealed class ApplicationNotificationAddedEventArgs(ApplicationNotificationEntry notification) : EventArgs
{
    /// <summary>Gets the stored notification.</summary>
    public ApplicationNotificationEntry Notification { get; } = notification;
}
