using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.Core.Replay;
using MissionPlanner.Library.Logging;

namespace MissionPlanner.App.Views.Logs;

public sealed partial class TelemetryLogsTabViewModel
{
    [RelayCommand(CanExecute = nameof(CanCopySnapshot))]
    private Task CopySnapshotAsync()
    {
        if (SelectedLog is not { } item)
        {
            return Task.CompletedTask;
        }

        var capturedAt = DateTimeOffset.UtcNow;
        return LibraryOperationAsync(async token =>
        {
            using var progress = await logDialogs.DisplayProgressCancellableAsync(
                () => "Copying telemetry log snapshot...",
                new DialogOptions
                {
                    Title = "Copy telemetry log",
                    RequestCancellation = () => operationCancellation?.Cancel()
                }, token);
            var text = await Task.Run(async () =>
            {
                // A separate stream keeps the packet browser and replay cursors untouched.
                await using var stream = await logStorage.OpenReadAsync(LogStorageArea.Telemetry, item.Id, token);
                var index = await logReader.IndexAsync(stream, item.Name, token);
                using var content = new MemoryStream();
                using (var writer = new Utf8JsonWriter(content, new JsonWriterOptions { Indented = true }))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("SchemaVersion", 1);
                    writer.WriteString("CapturedAt", capturedAt);
                    writer.WriteString("Scope", "All indexed packets in the selected recording, independent of display filters and page.");
                    writer.WritePropertyName("Recording");
                    JsonSerializer.Serialize(writer, item);
                    writer.WritePropertyName("Index");
                    JsonSerializer.Serialize(writer, new
                    {
                        index.SourceName,
                        index.Length,
                        index.StartedAt,
                        index.EndedAt,
                        index.Duration,
                        index.AdjustedTimestampCount,
                        PacketCount = index.Entries.Count
                    });
                    writer.WriteStartArray("Packets");
                    // Decode bounded batches while writing the complete clipboard document.
                    for (var end = index.Entries.Count; end > 0;)
                    {
                        token.ThrowIfCancellationRequested();
                        var start = Math.Max(0, end - 256);
                        var page = await packetBrowser.ReadPageAsync(stream, index, start,
                            new TelemetryPacketFilter(), end - start, token);
                        foreach (var row in page.Rows.Reverse())
                        {
                            JsonSerializer.Serialize(writer, row);
                        }
                        end = start;
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                return Encoding.UTF8.GetString(content.GetBuffer(), 0, checked((int)content.Length));
            }, token);
            token.ThrowIfCancellationRequested();
            await clipboard.SetTextAsync(text);
        });
    }

    private bool CanCopySnapshot => SelectedLog is not null && !IsBusy;
}
