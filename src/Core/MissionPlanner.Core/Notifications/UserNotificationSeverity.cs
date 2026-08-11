namespace MissionPlanner.Core.Notifications;

/// <summary>
/// Specifies the semantic severity of a user notification.
/// </summary>
public enum UserNotificationSeverity
{
    /// <summary>Indicates informational feedback.</summary>
    Information,

    /// <summary>Indicates a warning that may require attention.</summary>
    Warning,

    /// <summary>Indicates an operation failure.</summary>
    Error
}
