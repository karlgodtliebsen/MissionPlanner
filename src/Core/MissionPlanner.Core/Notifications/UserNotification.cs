using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

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
