namespace MissionPlanner.MavLink.Services;

/// <summary>
/// Defines the event topics for MAVLink events.
/// </summary>
public static class MavLinkEventTopics
{
    /// <summary>
    /// Occurs when a MAVLink message is received and successfully decoded.
    /// </summary>
    public const string ReceivedMessage = "mavlink.received-message";

    /// <summary>
    /// Occurs when a MAVLink frame is received.
    /// </summary>
    public const string ReceivedFrame = "mavlink.received-frame";

    /// <summary>
    /// Occurs after a new MAVLink message is received and processed.
    /// </summary>
    public const string NewMessage = "mavlink.new-message";
}
