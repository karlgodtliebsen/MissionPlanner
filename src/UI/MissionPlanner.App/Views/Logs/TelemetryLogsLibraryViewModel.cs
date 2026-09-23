using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.Core.Replay;
using MissionPlanner.Library.Logging;

namespace MissionPlanner.App.Views.Logs;

public sealed partial class TelemetryLogsTabViewModel
{
    private readonly ILogStorage logStorage;
    private readonly LogFileOperations logFiles;
    private readonly TelemetryPacketBrowser packetBrowser;
    private readonly ITelemetryLogReader logReader;
    private readonly TelemetryLogCatalog logCatalog;
    private readonly IFileSaveService fileSave;
    private readonly ILogFolderService logFolders;
    private readonly IDialogService logDialogs;
    private Stream? packetStream;
    private TelemetryLogIndex? packetIndex;
    private int pageStart;
    private int nextPacket;

    /// <summary>Stored recordings, newest first.</summary>
    public ObservableRangeCollection<TelemetryStoredLog> Logs { get; } = [];
    /// <summary>One bounded page of decoded packets.</summary>
    public ObservableRangeCollection<TelemetryPacketRow> Packets { get; } = [];
    /// <summary>Selected stored recording.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopySnapshotCommand))]
    public partial TelemetryStoredLog? SelectedLog
    {
        get; set;
    }
    /// <summary>Free text or raw hexadecimal packet search.</summary>
    [ObservableProperty]
    public partial string PacketSearch { get; set; } = "";
    /// <summary>Message identifier or name filter.</summary>
    [ObservableProperty]
    public partial string MessageFilter { get; set; } = "";
    /// <summary>Optional numeric system filter.</summary>
    [ObservableProperty]
    public partial string SystemFilter { get; set; } = "";
    /// <summary>Optional numeric component filter.</summary>
    [ObservableProperty]
    public partial string ComponentFilter { get; set; } = "";
    /// <summary>Optional maximum numeric MAV severity.</summary>
    [ObservableProperty]
    public partial string SeverityFilter { get; set; } = "";
    /// <summary>UTC timestamp to jump to.</summary>
    [ObservableProperty]
    public partial string JumpTimestamp { get; set; } = "";
    /// <summary>Whether packet pages track replay time.</summary>
    [ObservableProperty]
    public partial bool FollowReplay
    {
        get; set;
    }
    /// <summary>Packet count, duration, and page position.</summary>
    [ObservableProperty]
    public partial string PacketStatus { get; private set; } = "Open a recording to inspect packets.";
    /// <summary>Whether a desktop folder can be opened.</summary>
    public bool CanOpenLogFolder => logFolders.IsSupported;

    [RelayCommand]
    private Task RefreshLogsAsync()
    {
        return LibraryOperationAsync(async token =>
    {
        var items = await logCatalog.ListAsync(token);
        await Dispatcher.DispatchAsync(() => Logs.ReplaceRange(items.Where(item => item.Name.EndsWith(".tlog", StringComparison.OrdinalIgnoreCase))));
    });
    }

    [RelayCommand]
    private Task OpenStoredAsync()
    {
        return LibraryOperationAsync(async token =>
    {
        if (SelectedLog is { } item)
        {
            await OpenPacketsAsync(item, token);
        }
    });
    }

    private async Task OpenPacketsAsync(TelemetryStoredLog item, CancellationToken token)
    {
        using var progress = await logDialogs.DisplayProgressCancellableAsync(() => "Indexing telemetry packets...",
            new DialogOptions { Title = "Opening telemetry log", RequestCancellation = () => operationCancellation?.Cancel() }, token);
        var stream = await logStorage.OpenReadAsync(LogStorageArea.Telemetry, item.Id, token);
        try
        {
            var index = await logReader.IndexAsync(stream, item.Name, token);
            token.ThrowIfCancellationRequested();
            packetStream?.Dispose();
            packetStream = stream;
            packetIndex = index;
            var updated = await logCatalog.RefreshAsync(item, stream, index, token);
            await Dispatcher.DispatchAsync(() =>
            {
                var position = Logs.IndexOf(item);
                if (position >= 0)
                {
                    Logs[position] = updated;
                }
                SelectedLog = updated;
            });
            pageStart = 0;
            await ReadPacketPageAsync(0, token);
        }
        catch
        {
            if (!ReferenceEquals(packetStream, stream))
            {
                await stream.DisposeAsync();
            }

            throw;
        }
    }

    [RelayCommand]
    private Task ReplayStoredAsync()
    {
        return LibraryOperationAsync(async token =>
    {
        if (SelectedLog is not { } item)
        {
            return;
        }

        await OpenPacketsAsync(item, token);
        var stream = await logStorage.OpenReadAsync(LogStorageArea.Telemetry, item.Id, token);
        await replaySessionManager.LoadAsync(stream, item.Name, token);
    });
    }

    [RelayCommand]
    private Task StopReplayAsync()
    {
        return LibraryOperationAsync(async token =>
    {
        await replaySessionManager.PauseAsync(token);
        await replaySessionManager.SeekAsync(TimeSpan.Zero, token);
    });
    }

    [RelayCommand]
    private Task ExportLogAsync()
    {
        return LibraryOperationAsync(async token =>
    {
        if (SelectedLog is { } item)
        {
            await using var export = await logStorage.ExportAsync(LogStorageArea.Telemetry, item.Id, token);
            await fileSave.SaveAsync(export.FileName, export.Content, token);
        }
    });
    }

    [RelayCommand]
    private Task DeleteLogAsync()
    {
        return LibraryOperationAsync(async token =>
    {
        if (SelectedLog is not { } item)
        {
            return;
        }

        if (recordingService?.Current is { State: "Recording" } recording && recording.FilePath == item.Id)
        {
            throw new IOException("Disconnect before deleting the active recording.");
        }

        if (packetIndex?.SourceName == item.Name)
        {
            ClearPacketSelection();
        }

        await logStorage.DeleteAsync(LogStorageArea.Telemetry, item.Id, token);
        await Dispatcher.DispatchAsync(() =>
        {
            Logs.Remove(item);
            SelectedLog = null;
        });
    });
    }

    [RelayCommand]
    private Task OpenLogFolderAsync()
    {
        return logFolders.OpenAsync(LogStorageArea.Telemetry);
    }

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private void ClearPacketSelection()
    {
        packetStream?.Dispose();
        packetStream = null;
        packetIndex = null;
        Packets.Clear();
        SelectedLog = null;
        PacketStatus = "No packet log selected.";
    }

    [RelayCommand]
    private void CancelLogOperation()
    {
        operationCancellation?.Cancel();
    }

    [RelayCommand]
    private Task FilterPacketsAsync()
    {
        return LibraryOperationAsync(token => ReadPacketPageAsync(0, token));
    }

    [RelayCommand]
    private Task NextPacketsAsync()
    {
        return LibraryOperationAsync(token => ReadPacketPageAsync(nextPacket, token));
    }

    [RelayCommand]
    private Task PreviousPacketsAsync()
    {
        return LibraryOperationAsync(token => ReadPacketPageAsync(Math.Max(0, pageStart - 200), token));
    }

    [RelayCommand]
    private Task JumpPacketsAsync()
    {
        return LibraryOperationAsync(token =>
    {
        if (packetIndex is null)
        {
            return Task.CompletedTask;
        }

        return !DateTimeOffset.TryParse(JumpTimestamp, out var timestamp)
            ? throw new ArgumentException("Enter a UTC date/time, for example 2026-09-19T10:30:00Z.")
            : ReadPacketPageAsync(TelemetryPacketBrowser.FindIndex(packetIndex, timestamp), token);
    });
    }

    private async Task ReadPacketPageAsync(int start, CancellationToken token)
    {
        if (packetIndex is null || packetStream is null)
        {
            return;
        }

        byte? ParseByte(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : byte.Parse(text);
        }

        var filter = new TelemetryPacketFilter(PacketSearch, MessageFilter, ParseByte(SystemFilter),
            ParseByte(ComponentFilter), string.IsNullOrWhiteSpace(SeverityFilter) ? null : int.Parse(SeverityFilter));
        var page = await packetBrowser.ReadPageAsync(packetStream, packetIndex, start, filter, cancellationToken: token);
        token.ThrowIfCancellationRequested();
        pageStart = start;
        nextPacket = page.NextIndex;
        var status = $"{packetIndex.Entries.Count:N0} packets · {packetIndex.Duration:g} · {packetIndex.StartedAt:O} · showing {page.Rows.Count} (source {start}–{nextPacket})";
        await Dispatcher.DispatchAsync(() =>
        {
            Packets.ReplaceRange(page.Rows.OrderByDescending(row => row.Time).ThenByDescending(row => row.Index));
            PacketStatus = status;
        });
    }

    private Task LibraryOperationAsync(Func<CancellationToken, Task> operation)
    {
        if (IsBusy)
        {
            return Task.CompletedTask;
        }

        operationCancellation?.Dispose();
        operationCancellation = new CancellationTokenSource();
        return RunAsync(operationCancellation.Token, operation);
    }

    private void FollowPacketTime(ReplaySessionSnapshot snapshot)
    {
        if (FollowReplay && active && !IsBusy && packetIndex?.SourceName == snapshot.Index?.SourceName &&
            snapshot.Clock is { } clock && (Packets.Count == 0 || clock.LogTime > Packets[0].Time || clock.LogTime < Packets[^1].Time))
        {
            _ = LibraryOperationAsync(token => ReadPacketPageAsync(TelemetryPacketBrowser.FindIndex(packetIndex!, clock.LogTime), token));
        }
    }
}
