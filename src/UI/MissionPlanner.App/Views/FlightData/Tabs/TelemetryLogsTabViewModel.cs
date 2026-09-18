using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Projects isolated, read-only telemetry-log playback into the Flight Data workspace.</summary>
public sealed partial class TelemetryLogsTabViewModel : ViewModelBase
{
    private readonly IReplaySessionManager replaySessionManager;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IFileOpenService fileOpenService;

    private readonly TelemetryRecordingService? recordingService;
    private readonly MissionPlanner.Library.EventHub.Abstractions.IDomainEventHub? domainEvents;
    private IDisposable? recordingSubscription;
    private IDisposable? vehicleStateSubscription;
    private readonly IVehicleParameterRegistry? parameterRegistry;
    private CancellationTokenSource? operationCancellation;
    private bool active;


    /// <summary>Initializes the telemetry-log playback view model.</summary>
    /// <param name="replaySessionManager">Read-only replay session coordinator.</param>
    /// <param name="activeVehicle">Active-vehicle context used by the shared tab lifecycle.</param>
    /// <param name="fileOpenService">Avalonia file-selection boundary.</param>
    /// <param name="logger">Structured workflow logger.</param>
    /// <param name="logStorage">Platform log storage.</param>
    /// <param name="logFiles">Streaming import service.</param>
    /// <param name="packetBrowser">Bounded packet decoder.</param>
    /// <param name="logCatalog">Optional metadata catalog.</param>
    /// <param name="logReader">Classic tlog index reader.</param>
    /// <param name="fileSave">Platform export service.</param>
    /// <param name="logFolders">Desktop folder capability.</param>
    /// <param name="logDialogs">Cancellable loading progress.</param>
    /// <param name="recordingService">PC telemetry recording state.</param>
    /// <param name="domainEvents">Application recording lifecycle events.</param>
    /// <param name="parameterRegistry">Downloaded onboard logger configuration.</param>
    public TelemetryLogsTabViewModel(
        IReplaySessionManager replaySessionManager,
        IActiveVehicleContext activeVehicle,
        IFileOpenService fileOpenService,
        ILogger<TelemetryLogsTabViewModel> logger,
        MissionPlanner.Library.Logging.ILogStorage logStorage,
        MissionPlanner.Library.Logging.LogFileOperations logFiles,
        TelemetryPacketBrowser packetBrowser,
        ITelemetryLogReader logReader,
        TelemetryLogCatalog logCatalog,
        IFileSaveService fileSave,
        ILogFolderService logFolders,
        MissionPlanner.App.Utilities.Dialogs.IDialogService logDialogs,
        TelemetryRecordingService? recordingService = null,
        MissionPlanner.Library.EventHub.Abstractions.IDomainEventHub? domainEvents = null,
        IVehicleParameterRegistry? parameterRegistry = null)
        : base(logger)
    {
        this.logStorage = logStorage;
        this.logFiles = logFiles;
        this.packetBrowser = packetBrowser;
        this.logReader = logReader;
        this.logCatalog = logCatalog;
        this.fileSave = fileSave;
        this.logFolders = logFolders;
        this.logDialogs = logDialogs;
        this.parameterRegistry = parameterRegistry;
        this.recordingService = recordingService;
        this.domainEvents = domainEvents;
        this.replaySessionManager = replaySessionManager;
        this.activeVehicle = activeVehicle;
        this.fileOpenService = fileOpenService;
        SetMessages(ReplaySessionSnapshot.Unloaded.Message);
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        if (active)
        {
            return Task.CompletedTask;
        }
        active = true;
        recordingSubscription?.Dispose();
        recordingSubscription = domainEvents?.SubscribeDomainEventAsync<TelemetryRecordingChanged>((change, cancellationToken) =>
        {
            Dispatcher.Dispatch(() => ApplyRecording(recordingService?.Current ?? change.Status));
            return Task.CompletedTask;
        });
        if (recordingService is not null)
        {
            ApplyRecording(recordingService.Current);
        }
        vehicleStateSubscription?.Dispose();
        vehicleStateSubscription = domainEvents?.SubscribeDomainEventAsync<MissionPlanner.Core.DomainEvents.VehicleStateUpdated>((change, cancellationToken) =>
        {
            if (change.VehicleId == activeVehicle.VehicleId)
            {
                Dispatcher.Dispatch(ApplyOnboardLogging);
            }
            return Task.CompletedTask;
        });
        if (parameterRegistry is not null)
        {
            parameterRegistry.Changed += OnParameterChanged;
        }
        activeVehicle.Changed += OnActiveVehicleChanged;
        ApplyOnboardLogging();
        replaySessionManager.Changed += OnReplayChanged;
        ApplySnapshot(replaySessionManager.Snapshot);
        return RefreshLogsAsync();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        Deactivate();
        return Task.CompletedTask;
    }

    private void Deactivate()
    {
        active = false;
        operationCancellation?.Cancel();
        recordingSubscription?.Dispose();
        recordingSubscription = null;
        vehicleStateSubscription?.Dispose();
        vehicleStateSubscription = null;
        if (parameterRegistry is not null)
        {
            parameterRegistry.Changed -= OnParameterChanged;
        }
        activeVehicle.Changed -= OnActiveVehicleChanged;
        replaySessionManager.Changed -= OnReplayChanged;
    }
    /// <inheritdoc />
    public override void Dispose()
    {
        Deactivate();
        packetStream?.Dispose();
        base.Dispose();
    }


    /// <summary>Gets PC recording health, independent of vehicle onboard logging.</summary>
    [ObservableProperty]
    public partial string RecordingState { get; private set; } = "Idle";

    /// <summary>Gets the exact PC telemetry file location.</summary>
    [ObservableProperty]
    public partial string? RecordingPath { get; private set; }

    /// <summary>Gets a file or dropped-frame recording error.</summary>
    [ObservableProperty]
    public partial string? RecordingError { get; private set; }

    /// <summary>Gets vehicle logger health and configuration, independent of PC recording.</summary>
    [ObservableProperty]
    public partial string OnboardLoggingState { get; private set; } = "Unknown";

    /// <summary>Gets retained onboard logger and storage evidence.</summary>
    [ObservableProperty]
    public partial string? OnboardLoggingDetail { get; private set; }

    private void OnParameterChanged(MissionPlanner.Core.Vehicles.VehicleParameterChangedEventArgs args)
    {
        if (args.VehicleId == activeVehicle.VehicleId && args.Parameter?.Name == "LOG_BACKEND_TYPE")
        {
            Dispatcher.Dispatch(ApplyOnboardLogging);
        }
    }

    private void OnActiveVehicleChanged(MissionPlanner.Core.Vehicles.ActiveVehicleChangedEventArgs args)
    {
        Dispatcher.Dispatch(ApplyOnboardLogging);
    }

    private void ApplyOnboardLogging()
    {
        var evidence = activeVehicle.IsOnline ? activeVehicle.State?.OnboardLogging : null;
        var parameter = activeVehicle.VehicleId is { } id ? parameterRegistry?.GetParameter(id, "LOG_BACKEND_TYPE") : null;
        var status = (evidence ?? VehicleOnboardLoggingStatus.Empty).WithBackend(parameter is null ? null : (int)parameter.Value);
        OnboardLoggingState = $"{status.DisplayState} · LOG_BACKEND_TYPE={status.BackendType?.ToString() ?? "unknown"}" +
            (status.AffectsArming ? " · Blocking arming" : string.Empty);
        OnboardLoggingDetail = string.Join(Environment.NewLine, new[] { status.LatestMessage, status.StorageDetail }
            .Where(text => !string.IsNullOrWhiteSpace(text)).Distinct()) + Environment.NewLine + "Storage free space: unavailable";
    }

    private void ApplyRecording(TelemetryRecordingStatus status)
    {
        var state = status.State is "Idle" or "Completed" ? "Stopped" : status.State;
        RecordingState = $"{state} · {status.BytesWritten:N0} bytes" +
            (status.Started is { } started ? $" · started {started:u}" : string.Empty);
        RecordingPath = status.FilePath;
        RecordingError = status.Error;
    }

    /// <summary>Gets replay-only vehicle states; these vehicles never enter the live registry.</summary>
    public ObservableRangeCollection<VehicleState> ReplayVehicles
    {
        get;
    } = [];

    /// <summary>Gets the prominent source and safety label.</summary>
    [ObservableProperty]
    public partial string SourceModeLabel
    {
        get;
        private set;
    } = "LIVE / SIMULATION";

    /// <summary>Gets the current replay lifecycle label.</summary>
    [ObservableProperty]
    public partial string ReplayStateLabel
    {
        get;
        private set;
    } = "Unloaded";

    /// <summary>Gets the loaded telemetry-log display name.</summary>
    [ObservableProperty]
    public partial string SourceName
    {
        get;
        private set;
    } = "No telemetry log loaded";

    /// <summary>Gets whether a background file or playback operation is pending.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand), nameof(PlayPauseCommand), nameof(SeekCommand), nameof(CloseReplayCommand), nameof(ApplySpeedCommand))]
    public override partial bool IsBusy
    {
        get;
        set;
    }

    /// <summary>Gets whether a replay is loaded and every outbound MAVLink send is prohibited.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoad), nameof(CanControlReplay))]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand), nameof(PlayPauseCommand), nameof(SeekCommand), nameof(CloseReplayCommand), nameof(ApplySpeedCommand))]
    public partial bool IsReplayActive
    {
        get; private set;
    }

    /// <summary>Gets whether the replay clock is currently advancing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseText))]
    public partial bool IsPlaying
    {
        get; private set;
    }

    /// <summary>Gets playback progress from zero through one.</summary>
    [ObservableProperty]
    public new partial double Progress
    {
        get; private set;
    }

    /// <summary>Gets or sets the requested seek position in recorded seconds.</summary>
    [ObservableProperty]
    public partial double SeekSeconds
    {
        get;
        set;
    }

    /// <summary>Gets the total recorded duration in seconds.</summary>
    [ObservableProperty]
    public partial double DurationSeconds
    {
        get;
        private set;
    }

    /// <summary>Gets or sets the requested playback speed multiplier.</summary>
    [ObservableProperty]
    public partial double PlaybackSpeed { get; set; } = 1;

    /// <summary>Gets a readable current recorded timestamp.</summary>
    [ObservableProperty]
    public partial string ReplayTimeText
    {
        get;
        private set;
    } = "--";

    /// <summary>Gets decoded and rejected frame statistics.</summary>
    [ObservableProperty]
    public partial string FrameStatistics { get; private set; } = "0 decoded · 0 rejected";

    /// <summary>Gets the play/pause button label.</summary>
    public string PlayPauseText => IsPlaying ? "Pause" : "Play";

    /// <summary>Gets whether a new log can be loaded.</summary>
    public bool CanLoad => !IsBusy;

    /// <summary>Gets whether the loaded replay can be controlled.</summary>
    public bool CanControlReplay => IsReplayActive && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private async Task LoadAsync()
    {
        operationCancellation = new CancellationTokenSource();

        await RunAsync(operationCancellation.Token, async cancellationToken =>
        {
            var file = await fileOpenService.OpenAsync(
                "Select a Mission Planner telemetry log (.tlog)",
                ["*.tlog"],
                cancellationToken);
            if (file is null)
            {
                SetMessages("Telemetry-log selection cancelled.");
                return;
            }

            using (file)
            {
                var id = await logFiles.ImportAsync(MissionPlanner.Library.Logging.LogStorageArea.Telemetry,
                    file.FileName, file.Content, cancellationToken);
                var items = await logCatalog.ListAsync(cancellationToken);
                await Dispatcher.DispatchAsync(() =>
                {
                    Logs.ReplaceRange(items.Where(item => item.Name.EndsWith(".tlog", StringComparison.OrdinalIgnoreCase)));
                    SelectedLog = items.First(item => item.Id == id);
                });
                await OpenPacketsAsync(SelectedLog!, cancellationToken);
            }
        });
    }

    [RelayCommand(CanExecute = nameof(CanControlReplay))]
    private async Task PlayPauseAsync()
    {
        operationCancellation = new CancellationTokenSource();

        await RunAsync(operationCancellation.Token, async cancellationToken =>
        {
            if (replaySessionManager.Snapshot.State == ReplaySessionState.Playing)
            {
                await replaySessionManager.PauseAsync(cancellationToken);
            }
            else
            {
                await replaySessionManager.PlayAsync(cancellationToken);
            }
        });
    }

    [RelayCommand(CanExecute = nameof(CanControlReplay))]
    private async Task SeekAsync()
    {
        operationCancellation = new CancellationTokenSource();
        await RunAsync(operationCancellation.Token, (cancellationToken) => replaySessionManager.SeekAsync(TimeSpan.FromSeconds(SeekSeconds), cancellationToken));
    }

    [RelayCommand(CanExecute = nameof(CanControlReplay))]
    private async Task ApplySpeedAsync()
    {
        operationCancellation = new CancellationTokenSource();
        await RunAsync(operationCancellation.Token, (_) =>
        {
            replaySessionManager.SetSpeed(PlaybackSpeed);
            return Task.CompletedTask;
        });
    }

    [RelayCommand(CanExecute = nameof(CanControlReplay))]
    private async Task CloseReplayAsync()
    {
        operationCancellation = new CancellationTokenSource();
        await RunAsync(operationCancellation.Token, replaySessionManager.CloseAsync);
    }

    private void OnReplayChanged(ReplaySessionChangedEventArgs args)
    {
        Dispatcher.Dispatch(() => ApplySnapshot(args.Snapshot));
    }

    private void ApplySnapshot(ReplaySessionSnapshot snapshot)
    {
        IsReplayActive = snapshot.IsTransmissionProhibited;
        IsPlaying = snapshot.State == ReplaySessionState.Playing;
        SourceModeLabel = IsReplayActive ? "REPLAY · READ ONLY · SENDS DISABLED" : "LIVE / SIMULATION";
        ReplayStateLabel = snapshot.State.ToString();
        SourceName = snapshot.Index?.SourceName ?? "No telemetry log loaded";
        SetMessages(snapshot.Failure ?? snapshot.Message);
        Progress = snapshot.Progress;
        DurationSeconds = snapshot.Clock?.Duration.TotalSeconds ?? 0;
        SeekSeconds = snapshot.Clock?.Elapsed.TotalSeconds ?? 0;
        PlaybackSpeed = snapshot.Clock?.Speed ?? PlaybackSpeed;
        ReplayTimeText = snapshot.Clock is null
            ? "--"
            : $"{snapshot.Clock.LogTime:O} · {snapshot.Clock.Elapsed:g} / {snapshot.Clock.Duration:g}";
        FrameStatistics = $"{snapshot.DecodedFrames} decoded · {snapshot.RejectedFrames} rejected";

        ReplayVehicles.ReplaceRange(snapshot.Vehicles);

        OnPropertyChanged(nameof(CanLoad));
        OnPropertyChanged(nameof(CanControlReplay));
        LoadCommand.NotifyCanExecuteChanged();
        PlayPauseCommand.NotifyCanExecuteChanged();
        SeekCommand.NotifyCanExecuteChanged();
        CloseReplayCommand.NotifyCanExecuteChanged();
        ApplySpeedCommand.NotifyCanExecuteChanged();
        FollowPacketTime(snapshot);
    }

    private sealed class CallbackDisposable(Action callback) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            callback();
        }
    }
}
