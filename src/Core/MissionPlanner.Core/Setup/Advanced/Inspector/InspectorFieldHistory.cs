using System.Globalization;
using System.Text;

namespace MissionPlanner.Core.Setup.Advanced.Inspector;

/// <summary>A numeric value observed at the selected message's receive time.</summary>
/// <param name="ObservedAt">Time the latest message was received.</param>
/// <param name="Value">Finite numeric field value.</param>
public sealed record InspectorFieldSample(DateTimeOffset ObservedAt, double Value);

/// <summary>Bounded display-rate sampling; this is a trend view, not a lossless telemetry recorder.</summary>
public sealed class InspectorFieldHistory
{
    private readonly Queue<InspectorFieldSample> samples = new();
    private InspectorKey? key;
    private string? field;
    private long count;

    /// <summary>Gets a snapshot of up to 512 samples.</summary>
    public IReadOnlyList<InspectorFieldSample> Samples => samples.ToArray();

    /// <summary>Gets the source identity of the retained samples, if any.</summary>
    public InspectorKey? Source => key;

    /// <summary>Gets the selected decoded property name, including an array index when applicable.</summary>
    public string? Field => this.field;

    /// <summary>Returns units of the decoded representation; unknown fields are explicitly unspecified.</summary>
    public static string Units(InspectorKey? source, string? name)
    {
        if (name is null) return "unspecified";
        if (name.EndsWith("MetersPerSecond", StringComparison.Ordinal)) return "m/s";
        if (name.EndsWith("Degrees", StringComparison.Ordinal)) return "deg";
        if (name.EndsWith("Meters", StringComparison.Ordinal)) return "m";
        if (source?.MessageId == 30 && name is "Roll" or "Pitch" or "Yaw") return "rad";
        if (source?.MessageId == 74) return name switch
        {
            "Airspeed" or "Groundspeed" or "Climb" => "m/s", "Altitude" => "m", "Heading" => "deg", "Throttle" => "%", _ => "unspecified"
        };
        return "native (unspecified)";
    }

    /// <summary>Exports one immutable bounded series with source identity, receive times and decoded units.</summary>
    public byte[] ExportCsv()
    {
        var output = new StringBuilder("observed_at_utc,elapsed_seconds,direction,system_id,component_id,message_id,field,units,value\r\n");
        if (key is null || field is null || samples.Count == 0) return Encoding.UTF8.GetBytes(output.ToString());
        var start = samples.Peek().ObservedAt;
        static string Quote(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        foreach (var sample in samples)
        {
            output.Append(sample.ObservedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                .Append((sample.ObservedAt - start).TotalSeconds.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(Quote(key.Direction)).Append(',').Append(key.SystemId).Append(',').Append(key.ComponentId).Append(',').Append(key.MessageId).Append(',')
                .Append(Quote(field)).Append(',').Append(Quote(Units(key, field))).Append(',').Append(sample.Value.ToString("R", CultureInfo.InvariantCulture)).Append("\r\n");
        }
        return Encoding.UTF8.GetBytes(output.ToString());
    }

    /// <summary>Finds finite scalar fields suitable for plotting, using the decoder's invariant representation.</summary>
    public static IReadOnlyList<string> NumericFields(InspectorDetails details) => details.Fields
        .Where(pair => TryNumber(pair.Value, out _)).Select(pair => pair.Key).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Adds a new observation once; changing the selected message or field clears the previous series.</summary>
    public bool Observe(InspectorDetails details, string selectedField)
    {
        if (key != details.Row.Key || field != selectedField || details.Row.Count < count)
        {
            Clear();
            key = details.Row.Key;
            field = selectedField;
        }
        if (details.Row.Count == count || !details.Fields.TryGetValue(selectedField, out var raw) || !TryNumber(raw, out var value))
        {
            return false;
        }
        count = details.Row.Count;
        samples.Enqueue(new(details.Row.LastSeen, value));
        while (samples.Count > 512)
        {
            samples.Dequeue();
        }
        return true;
    }

    /// <summary>Removes the selected series and its duplicate-observation marker.</summary>
    public void Clear()
    {
        samples.Clear();
        key = null;
        field = null;
        count = 0;
    }

    private static bool TryNumber(string raw, out double value) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
}
