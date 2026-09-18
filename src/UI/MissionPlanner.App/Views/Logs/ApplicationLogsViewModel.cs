using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Logging;
using Serilog.Events;

namespace MissionPlanner.App.Views.Logs;

/// <summary>Displays batched structured live events and bounded historical file snapshots.</summary>
public sealed partial class ApplicationLogsViewModel : ViewModelBase
{
    private readonly ApplicationLogBuffer buffer;
    private readonly IApplicationLogLevelController levels;
    private readonly ApplicationLogHistory history;
    private readonly ApplicationLogFileState fileState;
    private readonly ILogStorage storage;
    private readonly IFileSaveService save;
    private readonly ITextClipboardService clipboard;
    private readonly ILogFolderService folders;
    private CancellationTokenSource? lifetime;
    private Task? pump;
    private int dirty = 1;
    private long clearedThrough;
    private IReadOnlyList<ApplicationLogEntry> sourceEntries = [];

    /// <summary>Initializes diagnostic services without subscribing until the view becomes visible.</summary>
    public ApplicationLogsViewModel(ApplicationLogBuffer buffer, IApplicationLogLevelController levels,
        ApplicationLogHistory history, ApplicationLogFileState fileState, ILogStorage storage,
        IFileSaveService save, ITextClipboardService clipboard, ILogFolderService folders,
        ILogger<ApplicationLogsViewModel> logger, IUiDispatcher dispatcher, IDomainEventHub events)
        : base(logger, dispatcher, events)
    {
        this.buffer = buffer;
        this.levels = levels;
        this.history = history;
        this.fileState = fileState;
        this.storage = storage;
        this.save = save;
        this.clipboard = clipboard;
        this.folders = folders;
        RuntimeLevel = levels.MinimumLevel;
    }

    /// <summary>Virtualized display rows, bounded by the memory/history capacity.</summary>
    public ObservableRangeCollection<ApplicationLogEntry> Entries { get; } = new();
    /// <summary>Stored diagnostic file metadata.</summary>
    public ObservableRangeCollection<LogStorageItem> Files { get; } = new();
    /// <summary>Available severity choices.</summary>
    public IReadOnlyList<LogEventLevel> Levels { get; } = Enum.GetValues<LogEventLevel>();
    /// <summary>Exact-level choices including no exact restriction.</summary>
    public IReadOnlyList<string> ExactLevels { get; } = ["Any", "Verbose", "Debug", "Information", "Warning", "Error", "Fatal"];

    /// <summary>Temporarily freezes UI ingestion, without changing the logger.</summary>
    [ObservableProperty]
    public partial bool Paused { get; set; }
    /// <summary>Scrolls to the latest event after each batch.</summary>
    [ObservableProperty]
    public partial bool FollowTail { get; set; } = true;
    /// <summary>True when the structured current-session buffer is selected.</summary>
    [ObservableProperty]
    public partial bool IsLive { get; private set; } = true;
    /// <summary>Current source description.</summary>
    [ObservableProperty]
    public partial string SourceDescription { get; private set; } = "Current session";
    /// <summary>Minimum display severity; does not affect recording.</summary>
    [ObservableProperty]
    public partial LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Verbose;
    /// <summary>Optional exact display severity.</summary>
    [ObservableProperty]
    public partial string ExactLevel { get; set; } = "Any";
    /// <summary>SourceContext filter.</summary>
    [ObservableProperty]
    public partial string SourceFilter { get; set; } = "";
    /// <summary>Free-text detail search.</summary>
    [ObservableProperty]
    public partial string Search { get; set; } = "";
    /// <summary>Optional inclusive timestamp lower bound.</summary>
    [ObservableProperty]
    public partial string FromTime { get; set; } = "";
    /// <summary>Optional inclusive timestamp upper bound.</summary>
    [ObservableProperty]
    public partial string UntilTime { get; set; } = "";
    /// <summary>Restricts display to exception-bearing events.</summary>
    [ObservableProperty]
    public partial bool ExceptionOnly { get; set; }
    /// <summary>The session's actual recording threshold.</summary>
    [ObservableProperty]
    public partial LogEventLevel RuntimeLevel { get; set; }
    /// <summary>Selected structured event.</summary>
    [ObservableProperty]
    public partial ApplicationLogEntry? SelectedEntry { get; set; }
    /// <summary>Selected historical file.</summary>
    [ObservableProperty]
    public partial LogStorageItem? SelectedFile { get; set; }
    /// <summary>Complete selected event details.</summary>
    [ObservableProperty]
    public partial string Details { get; private set; } = "";
    /// <summary>Current logger and storage health.</summary>
    [ObservableProperty]
    public partial string Health { get; private set; } = "";
    /// <summary>Whether verbose recording is currently enabled.</summary>
    public bool IsVerbose => RuntimeLevel == LogEventLevel.Verbose;
    /// <summary>Whether a native log folder can be opened.</summary>
    public bool CanOpenFolder => folders.IsSupported;
    /// <summary>Whether desktop file-management commands are available.</summary>
    public bool HasFileLogging => fileState.FileEnabled;

    partial void OnSelectedEntryChanged(ApplicationLogEntry? value)
        => Details = value is null ? "" : ApplicationLogFilter.Details(value);

    partial void OnRuntimeLevelChanged(LogEventLevel value)
    {
        levels.SetMinimumLevel(value);
        OnPropertyChanged(nameof(IsVerbose));
        UpdateHealth();
    }

    partial void OnPausedChanged(bool value)
    {
        if (!value)
        {
            RefreshLiveView();
        }
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        if (lifetime is not null)
        {
            return Task.CompletedTask;
        }

        lifetime = new CancellationTokenSource();
        buffer.Changed += BufferChanged;
        RefreshLiveView();
        pump = PumpAsync(lifetime.Token);
        return RefreshFilesAsync();
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        var previous = Interlocked.Exchange(ref lifetime, null);
        buffer.Changed -= BufferChanged;
        if (previous is null)
        {
            return;
        }

        previous.Cancel();
        if (pump is not null)
        {
            await pump;
        }

        previous.Dispose();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        buffer.Changed -= BufferChanged;
        lifetime?.Cancel();
        base.Dispose();
    }

    private void BufferChanged(object? sender, EventArgs args) => Interlocked.Exchange(ref dirty, 1);

    private async Task PumpAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                if (Interlocked.Exchange(ref dirty, 0) != 0)
                {
                    await Dispatcher.DispatchAsync(() =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            RefreshLiveView();
                        }
                    });
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    /// <summary>Copies a batch into the UI. Call on the UI dispatcher; paused or historical views remain unchanged.</summary>
    public void RefreshLiveView()
    {
        if (IsLive && !Paused)
        {
            sourceEntries = buffer.Snapshot(clearedThrough);
            ApplyFilters();
        }

        UpdateHealth();
    }

    private void UpdateHealth()
    {
        Health = $"Minimum: {levels.MinimumLevel} · memory {buffer.Count:N0}/{buffer.Capacity:N0} · " +
            (fileState.FileEnabled ? $"file: {fileState.CurrentFile}" : "file logging disabled");
    }

    [RelayCommand]
    private void ApplyFilters()
    {
        DateTimeOffset? Parse(string value) => DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
        var filter = new ApplicationLogFilter(MinimumLevel,
            Enum.TryParse<LogEventLevel>(ExactLevel, out var exact) ? exact : null,
            SourceFilter, Search, Parse(FromTime), Parse(UntilTime), ExceptionOnly);
        Entries.ReplaceRange(sourceEntries.Where(filter.Matches));
    }

    [RelayCommand]
    private void QuickFilter(string filter)
    {
        ExactLevel = "Any";
        MinimumLevel = filter == "Warnings" ? LogEventLevel.Warning :
            filter == "Errors" ? LogEventLevel.Error : LogEventLevel.Verbose;
        SourceFilter = filter switch
        {
            "MAVLink" => "MavLink",
            "Transport" => "Transport",
            "Parameters" => "Parameter",
            "Firmware" => "Firmware",
            _ => ""
        };
        ApplyFilters();
    }

    [RelayCommand]
    private void CurrentSession()
    {
        IsLive = true;
        SourceDescription = "Current session";
        Paused = false;
        RefreshLiveView();
    }

    [RelayCommand]
    private void ClearView()
    {
        if (IsLive)
        {
            clearedThrough = buffer.Snapshot().LastOrDefault()?.Sequence ?? clearedThrough;
        }

        sourceEntries = [];
        Entries.Clear();
        SelectedEntry = null;
    }

    [RelayCommand]
    private Task CopyAsync() => clipboard.SetTextAsync(SelectedEntry is null
        ? string.Join(Environment.NewLine, Entries.Select(ApplicationLogFilter.Details))
        : ApplicationLogFilter.Details(SelectedEntry));

    [RelayCommand]
    private Task ExportViewAsync() => FileOperationAsync(async token =>
    {
        var text = string.Join(Environment.NewLine, Entries.Select(ApplicationLogFilter.Details));
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(text));
        await save.SaveAsync("application-log-view.txt", content, token);
    });

    [RelayCommand]
    private Task ExportSelectedAsync() => FileOperationAsync(async token =>
    {
        if (SelectedEntry is null)
        {
            return;
        }

        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(ApplicationLogFilter.Details(SelectedEntry)));
        await save.SaveAsync("application-log-event.txt", content, token);
    });

    [RelayCommand]
    private Task RefreshFilesAsync() => FileOperationAsync(async token =>
    {
        var files = await history.ListAsync(token);
        await Dispatcher.DispatchAsync(() => Files.ReplaceRange(files));
    });

    [RelayCommand]
    private Task OpenFileAsync() => FileOperationAsync(async token =>
    {
        if (SelectedFile is not { } file)
        {
            return;
        }

        var entries = await history.ReadAsync(file.Id, buffer.Capacity, token);
        token.ThrowIfCancellationRequested();
        await Dispatcher.DispatchAsync(() =>
        {
            IsLive = false;
            SourceDescription = $"{file.Name} · {file.Size:N0} bytes · {file.Modified:u} · latest {entries.Count:N0} events";
            sourceEntries = entries;
            ApplyFilters();
        });
    });

    [RelayCommand]
    private Task ExportFileAsync() => FileOperationAsync(async token =>
    {
        if (SelectedFile is { } file)
        {
            await using var export = await storage.ExportAsync(LogStorageArea.Application, file.Id, token);
            await save.SaveAsync(export.FileName, export.Content, token);
        }
    });

    [RelayCommand]
    private Task DeleteFileAsync() => FileOperationAsync(async token =>
    {
        if (SelectedFile is { } file)
        {
            await history.DeleteAsync(file, token);
            await Dispatcher.DispatchAsync(() =>
            {
                Files.Remove(file);
                SelectedFile = null;
            });
        }
    });

    [RelayCommand]
    private Task OpenFolderAsync() => folders.OpenAsync(LogStorageArea.Application);

    private Task FileOperationAsync(Func<CancellationToken, Task> operation)
        => RunAsync(lifetime?.Token ?? CancellationToken.None, operation);
}
