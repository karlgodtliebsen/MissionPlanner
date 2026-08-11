namespace MissionPlanner.Core.Notifications;

/// <summary>
/// Specifies the preferred UI surface for a notification.
/// </summary>
public enum UserNotificationPresentation
{
    /// <summary>Requests short-lived toast feedback.</summary>
    Toast,

    /// <summary>Requests a persistent or actionable banner.</summary>
    Banner,

    /// <summary>Requests a modal dialog.</summary>
    Dialog
}
