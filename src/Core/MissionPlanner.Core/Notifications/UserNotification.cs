using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Notifications;

/// <summary>
/// Describes a user-facing notification without depending on a UI framework.
/// </summary>
/// <param name="Message">The notification message.</param>
/// <param name="Title">The optional notification title.</param>
/// <param name="Severity">The semantic severity.</param>
/// <param name="Presentation">The preferred presentation surface.</param>
/// <param name="VehicleId">The related vehicle, or <see langword="null"/> for an application-wide notification.</param>
public sealed record UserNotification(
    string Message,
    string? Title = null,
    UserNotificationSeverity Severity = UserNotificationSeverity.Information,
    UserNotificationPresentation Presentation = UserNotificationPresentation.Toast,
    VehicleId? VehicleId = null);

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
