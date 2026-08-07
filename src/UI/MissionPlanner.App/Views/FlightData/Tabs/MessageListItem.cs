namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>
/// Represents one display and export row in the Messages tab.
/// </summary>
/// <param name="identity">The source-qualified stable identity.</param>
/// <param name="origin"></param>
/// <param name="receivedAt"></param>
/// <param name="source"></param>
/// <param name="severity"></param>
/// <param name="severityDisplay"></param>
/// <param name="text"></param>
/// <param name="isAssembled"></param>
/// <param name="isTruncated"></param>
public sealed class MessageListItem(string identity, MessageListOrigin origin, DateTimeOffset receivedAt, string source, string severity, string severityDisplay, string text, bool isAssembled, bool isTruncated)
{
    public string Identity { get; } = identity;
    public MessageListOrigin Origin { get; } = origin;

    public DateTimeOffset ReceivedAt { get; } = receivedAt;
    public string Source { get; } = source;
    public string Severity { get; } = severity;
    public string SeverityDisplay { get; } = severityDisplay;
    public string Text { get; } = text;
    public bool IsAssembled { get; } = isAssembled;
    public bool IsTruncated { get; } = isTruncated;


    /// <summary>Gets a concise source/chunk detail for the row.</summary>
    public string Details => $"{ReceivedAt:O}  {Source}" +
                             (IsAssembled ? "  • assembled" : string.Empty) +
                             (IsTruncated ? "  • TRUNCATED" : string.Empty);
}


//public sealed record MessageListItem(string Identity, MessageListOrigin Origin, DateTimeOffset ReceivedAt, string Source, string Severity, string SeverityDisplay, string Text, bool IsAssembled, bool IsTruncated)
//{
//    /// <summary>Gets a concise source/chunk detail for the row.</summary>
//    public string Details => $"{ReceivedAt:O}  {Source}" +
//                             (IsAssembled ? "  • assembled" : string.Empty) +
//                             (IsTruncated ? "  • TRUNCATED" : string.Empty);
//}
