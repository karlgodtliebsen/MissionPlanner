using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Notifications;

/// <summary>
/// Represents a timestamped local application notification kept separately from MAVLink status text.
/// </summary>
/// <param name="Identity">The stable store identity.</param>
/// <param name="VehicleId">The related vehicle, when known.</param>
/// <param name="Title">The optional title.</param>
/// <param name="Message">The notification text.</param>
/// <param name="Severity">The local semantic severity.</param>
/// <param name="ReceivedAt">The time at which the notification was recorded.</param>
public sealed record ApplicationNotificationEntry(
    long Identity,
    VehicleId? VehicleId,
    string? Title,
    string Message,
    UserNotificationSeverity Severity,
    DateTimeOffset ReceivedAt);
