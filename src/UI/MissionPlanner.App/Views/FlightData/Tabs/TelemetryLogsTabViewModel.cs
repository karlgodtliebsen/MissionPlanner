using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Projects isolated, read-only telemetry-log playback into the Flight Data workspace.</summary>
public sealed partial class TelemetryLogsTabViewModel : ObservableObject, IDisposable
{
    private readonly IReplaySessionManager replaySessionManager;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<TelemetryLogsTabViewModel> logger;
    private bool disposed;

    /// <summary>Initializes the telemetry-log playback view model.</summary>
    /// <param name="replaySessionManager">Read-only replay session coordinator.</param>
    /// <param name="dispatcher">UI dispatcher.</param>
    /// <param name="logger">Structured workflow logger.</param>
    public TelemetryLogsTabViewModel(
        IReplaySessionManager replaySessionManager,
        IDispatcher dispatcher,
        ILogger<TelemetryLogsTabViewModel> logger)
    {
        this.replaySessionManager = replaySessionManager;
        this.dispatcher = dispatcher;
        this.logger = logger;
        replaySessionManager.Changed += OnReplayChanged;
        ApplySnapshot(replaySessionManager.Snapshot);
    }

    /// <summary>Gets replay-only vehicle states; these vehicles never enter the live registry.</summary>
    public ObservableCollection<VehicleState> ReplayVehicles { get; } = [];

    /// <summary>Gets the prominent source and safety label.</summary>
    [ObservableProperty]
    public partial string SourceModeLabel { get; private set; } = "LIVE / SIMULATION";

    /// <summary>Gets the current replay lifecycle label.</summary>
    [ObservableProperty]
    public partial string ReplayStateLabel { get; private set; } = "Unloaded";

    /// <summary>Gets the loaded telemetry-log display name.</summary>
    [ObservableProperty]
    public partial string SourceName { get; private set; } = "No telemetry log loaded";

    /// <summary>Gets workflow status or failure detail.</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; private set; } = ReplaySessionSnapshot.Unloaded.Message;

    /// <summary>Gets whether a background file or playback operation is pending.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand), nameof(PlayPauseCommand), nameof(SeekCommand), nameof(CloseReplayCommand), nameof(ApplySpeedCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Gets whether a replay is loaded and every outbound MAVLink send is prohibited.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoad), nameof(CanControlReplay))]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand), nameof(PlayPauseCommand), nameof(SeekCommand), nameof(CloseReplayCommand), nameof(ApplySpeedCommand))]
    public partial bool IsReplayActive { get; private set; }

    /// <summary>Gets whether the replay clock is currently advancing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseText))]
    public partial bool IsPlaying { get; private set; }

    /// <summary>Gets playback progress from zero through one.</summary>
    [ObservableProperty]
    public partial double Progress { get; private set; }

    /// <summary>Gets or sets the requested seek position in recorded seconds.</summary>
    [ObservableProperty]
    public partial double SeekSeconds { get; set; }

    /// <summary>Gets the total recorded duration in seconds.</summary>
    [ObservableProperty]
    public partial double DurationSeconds { get; private set; }

    /// <summary>Gets or sets the requested playback speed multiplier.</summary>
    [ObservableProperty]
    public partial double PlaybackSpeed { get; set; } = 1;

    /// <summary>Gets a readable current recorded timestamp.</summary>
    [ObservableProperty]
    public partial string ReplayTimeText { get; private set; } = "--";

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
        await RunAsync(async cancellationToken =>
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select a Mission Planner telemetry log (.tlog)"
            });
            if (file is null)
            {
                StatusMessage = "Telemetry-log selection cancelled.";
                return;
            }

            var selectedStream = await file.OpenReadAsync();
            Stream ownedStream = selectedStream;
            if (!selectedStream.CanSeek)
            {
                var temporaryPath = Path.Combine(Path.GetTempPath(), $"missionplanner-replay-{Guid.NewGuid():N}.tlog");
                var temporaryStream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.Read,
                    65_536,
                    FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                await selectedStream.CopyToAsync(temporaryStream, cancellationToken);
                await selectedStream.DisposeAsync();
                temporaryStream.Position = 0;
                ownedStream = temporaryStream;
            }

            await replaySessionManager.LoadAsync(ownedStream, file.FileName, cancellationToken);
        });
    }

    [RelayCommand(CanExecute = nameof(CanControlReplay))]
    private Task PlayPauseAsync() => RunAsync(async cancellationToken =>
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

    [RelayCommand(CanExecute = nameof(CanControlReplay))]
    private Task SeekAsync() => RunAsync(cancellationToken =>
        replaySessionManager.SeekAsync(TimeSpan.FromSeconds(SeekSeconds), cancellationToken));

    [RelayCommand(CanExecute = nameof(CanControlReplay))]
    private Task ApplySpeedAsync() => RunAsync(_ =>
    {
        replaySessionManager.SetSpeed(PlaybackSpeed);
        return Task.CompletedTask;
    });

    [RelayCommand(CanExecute = nameof(CanControlReplay))]
    private Task CloseReplayAsync() => RunAsync(replaySessionManager.CloseAsync);

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        replaySessionManager.Changed -= OnReplayChanged;
        disposed = true;
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await operation(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Replay operation cancelled.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Telemetry-log replay operation failed.");
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnReplayChanged(object? sender, ReplaySessionChangedEventArgs args)
    {
        dispatcher.Dispatch(() => ApplySnapshot(args.Snapshot));
    }

    private void ApplySnapshot(ReplaySessionSnapshot snapshot)
    {
        IsReplayActive = snapshot.IsTransmissionProhibited;
        IsPlaying = snapshot.State == ReplaySessionState.Playing;
        SourceModeLabel = IsReplayActive ? "REPLAY · READ ONLY · SENDS DISABLED" : "LIVE / SIMULATION";
        ReplayStateLabel = snapshot.State.ToString();
        SourceName = snapshot.Index?.SourceName ?? "No telemetry log loaded";
        StatusMessage = snapshot.Failure ?? snapshot.Message;
        Progress = snapshot.Progress;
        DurationSeconds = snapshot.Clock?.Duration.TotalSeconds ?? 0;
        SeekSeconds = snapshot.Clock?.Elapsed.TotalSeconds ?? 0;
        PlaybackSpeed = snapshot.Clock?.Speed ?? PlaybackSpeed;
        ReplayTimeText = snapshot.Clock is null
            ? "--"
            : $"{snapshot.Clock.LogTime:O} · {snapshot.Clock.Elapsed:g} / {snapshot.Clock.Duration:g}";
        FrameStatistics = $"{snapshot.DecodedFrames} decoded · {snapshot.RejectedFrames} rejected";

        ReplayVehicles.Clear();
        foreach (var vehicle in snapshot.Vehicles)
        {
            ReplayVehicles.Add(vehicle);
        }

        OnPropertyChanged(nameof(CanLoad));
        OnPropertyChanged(nameof(CanControlReplay));
        LoadCommand.NotifyCanExecuteChanged();
        PlayPauseCommand.NotifyCanExecuteChanged();
        SeekCommand.NotifyCanExecuteChanged();
        CloseReplayCommand.NotifyCanExecuteChanged();
        ApplySpeedCommand.NotifyCanExecuteChanged();
    }
}
