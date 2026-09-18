using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Serilog.Events;

namespace MissionPlanner.Library.Logging;

/// <summary>Reads existing text diagnostic files through storage and protects active file deletion.</summary>
public sealed partial class ApplicationLogHistory(ILogStorage storage, ApplicationLogFileState state)
{
    /// <summary>Lists stored application files newest first.</summary>
    public Task<IReadOnlyList<LogStorageItem>> ListAsync(CancellationToken cancellationToken = default)
        => storage.ListAsync(LogStorageArea.Application, cancellationToken);

    /// <summary>Deletes an old file only after ruling out the active rolling interval.</summary>
    public Task DeleteAsync(LogStorageItem item, CancellationToken cancellationToken = default)
    {
        if (state.IsActive(item))
        {
            throw new IOException("The active application log cannot be deleted.");
        }

        return storage.DeleteAsync(LogStorageArea.Application, item.Id, cancellationToken);
    }

    /// <summary>Reads the newest bounded window of text events. Partial or unrecognized final lines are tolerated.</summary>
    public async Task<IReadOnlyList<ApplicationLogEntry>> ReadAsync(string id, int capacity = 5000,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        await using var stream = await storage.OpenReadAsync(LogStorageArea.Application, id, cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var entries = new Queue<ApplicationLogEntry>();
        ApplicationLogEntry? current = null;
        var continuation = new StringBuilder();
        long sequence = 0;
        void AddCurrent()
        {
            if (current is null)
            {
                return;
            }

            if (continuation.Length != 0)
            {
                var properties = current.Properties.ToDictionary(pair => pair.Key, pair => pair.Value);
                properties["HistoricalException"] = new ScalarValue(continuation.ToString());
                current = current with { Properties = properties };
            }

            entries.Enqueue(current);
            while (entries.Count > capacity)
            {
                entries.Dequeue();
            }

            continuation.Clear();
        }

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = Header().Match(line);
            if (match.Success && DateTimeOffset.TryParse(match.Groups["time"].Value, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var timestamp) && TryLevel(match.Groups["level"].Value, out var level))
            {
                AddCurrent();
                var source = match.Groups["source"].Value;
                current = new(++sequence, timestamp, level, "", match.Groups["message"].Value,
                    string.IsNullOrEmpty(source) ? null : source, null,
                    new Dictionary<string, LogEventPropertyValue> { ["ThreadId"] = new ScalarValue(match.Groups["thread"].Value) });
            }
            else if (current is not null && continuation.Length < 65536 &&
                     (line.StartsWith("   ", StringComparison.Ordinal) || line.Contains("Exception", StringComparison.Ordinal) ||
                      line.StartsWith(" --->", StringComparison.Ordinal) || line.StartsWith("--- End", StringComparison.Ordinal)))
            {
                continuation.AppendLine(line.Length <= 8192 ? line : line[..8192]);
            }

            if (sequence % 256 == 0)
            {
                await Task.Yield();
            }
        }

        AddCurrent();
        return entries.ToArray();
    }

    private static bool TryLevel(string abbreviation, out LogEventLevel level)
    {
        level = abbreviation switch
        {
            "VRB" => LogEventLevel.Verbose,
            "DBG" => LogEventLevel.Debug,
            "INF" => LogEventLevel.Information,
            "WRN" => LogEventLevel.Warning,
            "ERR" => LogEventLevel.Error,
            "FTL" => LogEventLevel.Fatal,
            _ => (LogEventLevel)(-1)
        };
        return Enum.IsDefined(level);
    }

    [GeneratedRegex(@"^(?<time>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+ [+-]\d{2}:\d{2}) \[(?<level>[A-Z]{3})\] \((?<thread>[^)]*)\)(?: \[(?<source>[^\]]*)\])? (?<message>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex Header();
}
