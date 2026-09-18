using Serilog.Events;

namespace MissionPlanner.Library.Logging;

/// <summary>Structured predicates shared by live and historical diagnostic views.</summary>
/// <param name="Minimum">Lowest included severity.</param>
/// <param name="Exact">Optional exact severity.</param>
/// <param name="Source">Category fragment.</param>
/// <param name="Search">Rendered message, template, exception, or property search.</param>
/// <param name="From">Inclusive lower timestamp bound.</param>
/// <param name="Until">Inclusive upper timestamp bound.</param>
/// <param name="ExceptionOnly">Restricts results to events with exception details.</param>
public sealed record ApplicationLogFilter(LogEventLevel Minimum = LogEventLevel.Verbose, LogEventLevel? Exact = null,
    string? Source = null, string? Search = null, DateTimeOffset? From = null, DateTimeOffset? Until = null, bool ExceptionOnly = false)
{
    /// <summary>Tests an entry using structured level, category, and timestamp data.</summary>
    public bool Matches(ApplicationLogEntry entry)
        => entry.Level >= Minimum && (Exact is null || entry.Level == Exact)
           && (string.IsNullOrWhiteSpace(Source) || (entry.SourceContext?.Contains(Source, StringComparison.OrdinalIgnoreCase) ?? false))
           && (From is null || entry.Timestamp >= From) && (Until is null || entry.Timestamp <= Until)
           && (!ExceptionOnly || entry.Exception is not null || entry.Properties.ContainsKey("HistoricalException"))
           && (string.IsNullOrWhiteSpace(Search) || Details(entry).Contains(Search, StringComparison.OrdinalIgnoreCase));

    /// <summary>Formats complete entry details for display, copying, and explicit export.</summary>
    public static string Details(ApplicationLogEntry entry)
        => $"{entry.Timestamp:O} [{entry.Level}] {entry.SourceContext}\n{entry.RenderedMessage}\nTemplate: {entry.MessageTemplate}\n{entry.Exception}\n" +
           string.Join("\n", entry.Properties.Select(property => $"{property.Key}: {property.Value}"));
}
