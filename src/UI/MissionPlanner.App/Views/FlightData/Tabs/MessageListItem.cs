namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>
/// Represents one display and export row in the Messages tab.
/// </summary>
/// <param name="Identity">The source-qualified stable identity.</param>
/// <param name="Origin">The source stream.</param>
/// <param name="ReceivedAt">The complete timestamp.</param>
/// <param name="Source">The source identity.</param>
/// <param name="Severity">The filterable severity name.</param>
/// <param name="SeverityDisplay">The severity label and non-color marker.</param>
/// <param name="Text">The message text.</param>
/// <param name="IsAssembled">Whether MAVLink chunks were assembled.</param>
/// <param name="IsTruncated">Whether the message is explicitly incomplete.</param>
public sealed record MessageListItem(
    string Identity,
    MessageListOrigin Origin,
    DateTimeOffset ReceivedAt,
    string Source,
    string Severity,
    string SeverityDisplay,
    string Text,
    bool IsAssembled,
    bool IsTruncated)
{
    /// <summary>Gets a concise source/chunk detail for the row.</summary>
    public string Details => $"{ReceivedAt:O}  {Source}" +
                             (IsAssembled ? "  • assembled" : string.Empty) +
                             (IsTruncated ? "  • TRUNCATED" : string.Empty);
}
