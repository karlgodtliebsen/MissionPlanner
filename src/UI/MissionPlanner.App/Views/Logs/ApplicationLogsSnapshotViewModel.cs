using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace MissionPlanner.App.Views.Logs;

public sealed partial class ApplicationLogsViewModel
{
    [RelayCommand]
    private Task CopySnapshotAsync()
    {
        var capturedAt = DateTimeOffset.UtcNow;
        var source = SourceDescription;
        var live = IsLive;
        var entries = live ? buffer.Snapshot() : sourceEntries.ToArray();
        return FileOperationAsync(async token =>
        {
            var text = await Task.Run(() => JsonSerializer.Serialize(new
            {
                SchemaVersion = 1,
                CapturedAt = capturedAt,
                Source = source,
                Scope = live
                    ? "All retained current-session events, independent of selection, filters, pause and clear view."
                    : "All loaded historical events, independent of selection and filters.",
                Capacity = buffer.Capacity,
                RuntimeLevel = levels.MinimumLevel.ToString(),
                Events = entries.OrderByDescending(entry => entry.Timestamp).ThenByDescending(entry => entry.Sequence)
                    .Select(entry => new
                    {
                        entry.Sequence,
                        entry.Timestamp,
                        Level = entry.Level.ToString(),
                        entry.MessageTemplate,
                        entry.RenderedMessage,
                        entry.SourceContext,
                        Exception = entry.Exception?.ToString(),
                        Properties = SnapshotProperties(entry.Properties)
                    })
            }, new JsonSerializerOptions { WriteIndented = true }), token);
            token.ThrowIfCancellationRequested();
            await clipboard.SetTextAsync(text);
        });
    }

    private static JsonElement SnapshotProperties(IReadOnlyDictionary<string, LogEventPropertyValue> properties)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        new JsonValueFormatter().Format(new StructureValue(properties.Select(
            pair => new LogEventProperty(pair.Key, pair.Value))), output);
        return JsonSerializer.Deserialize<JsonElement>(output.ToString());
    }
}
