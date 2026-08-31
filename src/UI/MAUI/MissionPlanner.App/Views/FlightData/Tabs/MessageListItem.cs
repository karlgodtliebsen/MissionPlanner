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
    /// <summary>Gets the source-qualified stable message identity.</summary>
    public string Identity { get; } = identity;
    /// <summary>Gets the message origin.</summary>
    public MessageListOrigin Origin { get; } = origin;

    /// <summary>Gets when the message was received.</summary>
    public DateTimeOffset ReceivedAt { get; } = receivedAt;
    /// <summary>Gets the display name of the message source.</summary>
    public string Source { get; } = source;
    /// <summary>Gets the normalized severity value.</summary>
    public string Severity { get; } = severity;
    /// <summary>Gets the localized or presentation-ready severity text.</summary>
    public string SeverityDisplay { get; } = severityDisplay;
    /// <summary>Gets the message text.</summary>
    public string Text { get; } = text;
    /// <summary>Gets whether the text was assembled from multiple message chunks.</summary>
    public bool IsAssembled { get; } = isAssembled;
    /// <summary>Gets whether the message text was truncated.</summary>
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
