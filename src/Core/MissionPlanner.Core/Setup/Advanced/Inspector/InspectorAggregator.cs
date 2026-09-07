using System.Collections;
using System.Globalization;
using System.Reflection;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Setup.Advanced.Inspector;

/// <summary>Aggregates exact observations using bounded keys and fixed-size rate buckets.</summary>
public sealed class InspectorAggregator(IMavLinkMessageDefinitionRegistry definitions, TimeProvider clock)
{
    /// <summary>Maximum retained direction/system/component/message combinations.</summary>
    public const int MaximumKeys = 512;
    private readonly object sync = new();
    private readonly Dictionary<InspectorKey, Entry> entries = [];
    private long omitted;

    /// <summary>Gets observations omitted because the key capacity or frame-size limit was exceeded.</summary>
    public long Omitted => Interlocked.Read(ref omitted);

    /// <summary>Counts an observation without UI work or retaining a frame history.</summary>
    public void Observe(MavLinkInspectionObservation observation)
    {
        var frame = observation.Frame;
        var key = new InspectorKey(observation.Direction.ToString(), frame.SystemId, frame.ComponentId, frame.MessageId);
        lock (sync)
        {
            if (frame.RawBytes.Length > 280)
            {
                omitted++;
                return;
            }
            var now = clock.GetUtcNow();
            if (!entries.TryGetValue(key, out var entry))
            {
                if (entries.Count >= MaximumKeys)
                {
                    omitted++;
                    return;
                }
                entry = new Entry(key, definitions.TryGet(frame.MessageId, out var definition) ? definition.Name : "Unknown", now);
                entries.Add(key, entry);
            }
            entry.Count++;
            entry.Bytes += frame.RawBytes.Length;
            entry.LastSeen = now;
            entry.Latest = observation;
            var second = now.ToUnixTimeSeconds();
            var slot = (int)((second % 5 + 5) % 5);
            if (entry.Seconds[slot] != second)
            {
                entry.Seconds[slot] = second;
                entry.Counts[slot] = 0;
                entry.ByteCounts[slot] = 0;
            }
            entry.Counts[slot]++;
            entry.ByteCounts[slot] += frame.RawBytes.Length;
        }
    }

    /// <summary>Gets filtered statistics without mutating collection state.</summary>
    public IReadOnlyList<InspectorRow> Rows(string? search = null)
    {
        lock (sync)
        {
            var now = clock.GetUtcNow();
            return entries.Values.Select(entry => Row(entry, now))
                .Where(row => Matches(row, search)).OrderBy(row => row.Key.MessageId)
                .ThenBy(row => row.Key.SystemId).ThenBy(row => row.Key.ComponentId)
                .ThenBy(row => row.Key.Direction, StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>Gets exact raw bytes and already-decoded fields for a selected key.</summary>
    public InspectorDetails? Details(InspectorKey key)
    {
        lock (sync)
        {
            return entries.TryGetValue(key, out var entry) ? Details(entry, clock.GetUtcNow()) : null;
        }
    }

    /// <summary>Captures all retained aggregates and their latest values for explicit export.</summary>
    public InspectorSnapshot Export(long dropped)
    {
        lock (sync)
        {
            var now = clock.GetUtcNow();
            return new(now, dropped + omitted, entries.Values.Select(entry => Details(entry, now)).ToArray());
        }
    }

    /// <summary>Resets statistics, retaining no previous raw observations.</summary>
    public void Clear()
    {
        lock (sync)
        {
            entries.Clear();
            omitted = 0;
        }
    }

    /// <summary>Matches free text or exact id:, sys:, comp:, and dir: tokens, combined with AND.</summary>
    public static bool Matches(InspectorRow row, string? search)
    {
        foreach (var token in (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = token.Split(':', 2);
            var matched = parts.Length == 2 ? parts[0].ToLowerInvariant() switch
            {
                "id" => uint.TryParse(parts[1], out var id) && row.Key.MessageId == id,
                "sys" => byte.TryParse(parts[1], out var system) && row.Key.SystemId == system,
                "comp" => byte.TryParse(parts[1], out var component) && row.Key.ComponentId == component,
                "dir" => row.Key.Direction.Equals(parts[1], StringComparison.OrdinalIgnoreCase),
                _ => false
            } : row.Name.Contains(token, StringComparison.OrdinalIgnoreCase)
                || row.Key.MessageId.ToString(CultureInfo.InvariantCulture) == token
                || row.Key.Direction.Contains(token, StringComparison.OrdinalIgnoreCase);
            if (!matched)
            {
                return false;
            }
        }
        return true;
    }

    private static InspectorRow Row(Entry entry, DateTimeOffset now)
    {
        var latest = entry.Latest!;
        var raw = latest.Frame.RawBytes.Span;
        var signed = raw.Length > 2 && raw[0] == 0xFD && (raw[2] & 1) != 0;
        var second = now.ToUnixTimeSeconds();
        long count = 0;
        long bytes = 0;
        for (var index = 0; index < 5; index++)
        {
            if (entry.Seconds[index] <= second && entry.Seconds[index] > second - 5)
            {
                count += entry.Counts[index];
                bytes += entry.ByteCounts[index];
            }
        }
        var verification = (latest.CrcVerified ? "CRC verified" : "CRC unverified")
            + (signed ? "; signature present, authentication unverified" : "; unsigned");
        return new(entry.Key, entry.Name, entry.Count, entry.Bytes, entry.FirstSeen, entry.LastSeen,
            count / 5d, bytes / 5d, latest.Frame.Payload.Length, latest.Frame.Sequence, verification);
    }

    private static InspectorDetails Details(Entry entry, DateTimeOffset now)
    {
        var fields = new Dictionary<string, string>();
        var message = entry.Latest!.Message;
        if (message is not null)
        {
            var characterBudget = 8192;
            foreach (var property in message.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Take(128))
            {
                if (property.GetIndexParameters().Length == 0 && property.Name is not ("EndPoint" or "ReceivedAt"))
                {
                    var value = Format(property.GetValue(message));
                    characterBudget -= property.Name.Length + value.Length;
                    if (characterBudget < 0)
                    {
                        fields["Omitted fields"] = "Decoded detail limit reached (8192 characters). Raw frame remains complete.";
                        break;
                    }
                    fields[property.Name] = value;
                }
            }
        }
        return new(Row(entry, now), Convert.ToHexString(entry.Latest.Frame.RawBytes.Span), fields);
    }

    private static string Format(object? value)
    {
        var text = value switch
        {
            null => "unavailable",
            string content => content,
            IEnumerable values => string.Join(", ", values.Cast<object?>().Take(256).Select(item => Convert.ToString(item, CultureInfo.InvariantCulture))),
            IFormattable formatted => formatted.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };
        return text.Length > 2048 ? text[..2048] + " (truncated)" : text;
    }

    private sealed class Entry(InspectorKey key, string name, DateTimeOffset first)
    {
        internal InspectorKey Key { get; } = key;
        internal string Name { get; } = name;
        internal DateTimeOffset FirstSeen { get; } = first;
        internal DateTimeOffset LastSeen { get; set; }
        internal long Count { get; set; }
        internal long Bytes { get; set; }
        internal MavLinkInspectionObservation? Latest { get; set; }
        internal long[] Seconds { get; } = new long[5];
        internal long[] Counts { get; } = new long[5];
        internal long[] ByteCounts { get; } = new long[5];
    }
}
