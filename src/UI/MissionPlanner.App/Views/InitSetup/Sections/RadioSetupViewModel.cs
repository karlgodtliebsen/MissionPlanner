using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.DateTime.Domain;

namespace MissionPlanner.App.Views.InitSetup.Sections;

/// <summary>Projects live RC channels and the radio endpoint-calibration state machine into Setup controls.</summary>
public sealed partial class RadioSetupViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IRadioCalibrationService radioService;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly ISetupCompletionStore completionStore;
    private readonly ISetupWorkflowCatalog workflowCatalog;
    private readonly IUserConfirmationService confirmation;
    private readonly IDateTimeProvider clock;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<RadioSetupViewModel> logger;
    private CancellationTokenSource? operationCancellation;

    /// <summary>Initializes the radio Setup workflow.</summary>
    /// <param name="descriptor">The radio workflow descriptor.</param>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="radioService">The radio calibration service.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="completionStore">The Setup evidence store.</param>
    /// <param name="workflowCatalog">The Setup workflow catalog.</param>
    /// <param name="confirmation">The shared confirmation service.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public RadioSetupViewModel(
        SetupWorkflowDescriptor descriptor,
        IActiveVehicleContext activeVehicle,
        IRadioCalibrationService radioService,
        IVehicleParameterRegistry parameterRegistry,
        ISetupCompletionStore completionStore,
        ISetupWorkflowCatalog workflowCatalog,
        IUserConfirmationService confirmation,
        IDateTimeProvider clock,
        IDispatcher dispatcher,
        ILogger<RadioSetupViewModel> logger)
        : base(descriptor)
    {
        this.activeVehicle = activeVehicle;
        this.radioService = radioService;
        this.parameterRegistry = parameterRegistry;
        this.completionStore = completionStore;
        this.workflowCatalog = workflowCatalog;
        this.confirmation = confirmation;
        this.clock = clock;
        this.dispatcher = dispatcher;
        this.logger = logger;
        radioService.StateChanged += OnCalibrationStateChanged;
        activeVehicle.Changed += OnActiveVehicleChanged;
        Show(radioService.Current);
        RefreshLiveChannels();
    }

    /// <summary>Gets the live RC channels.</summary>
    public ObservableCollection<RadioChannelDisplayViewModel> Channels { get; } = [];

    /// <summary>Gets the current configuration and validation issues.</summary>
    public ObservableCollection<string> Issues { get; } = [];

    /// <summary>Gets whether the RC telemetry is stale.</summary>
    [ObservableProperty]
    public partial bool IsStale { get; private set; } = true;

    /// <summary>Gets the current calibration workflow stage.</summary>
    [ObservableProperty]
    public partial RadioCalibrationState CalibrationState { get; private set; }

    /// <summary>Gets the primary calibration instruction.</summary>
    [ObservableProperty]
    public partial string Instruction { get; private set; } = string.Empty;

    /// <summary>Gets a summary of captured endpoints during calibration.</summary>
    [ObservableProperty]
    public partial string CaptureSummary { get; private set; } = string.Empty;

    /// <summary>Gets whether any live channels are available.</summary>
    public bool HasChannels => Channels.Count > 0;

    /// <summary>Gets whether any configuration or validation issues exist.</summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>Gets whether calibration capture can start.</summary>
    public bool CanStart => CalibrationState is RadioCalibrationState.NotStarted or RadioCalibrationState.Success or
        RadioCalibrationState.Failed or RadioCalibrationState.Cancelled or RadioCalibrationState.Disconnected;

    /// <summary>Gets whether calibration capture is active.</summary>
    public bool CanFinish => CalibrationState == RadioCalibrationState.Capturing;

    /// <inheritdoc />
    public override void Cancel()
    {
        if (CanFinish)
        {
            _ = radioService.CancelAsync();
        }

        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        radioService.StateChanged -= OnCalibrationStateChanged;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        radioService.Dispose();
        base.Dispose();
    }

    private bool CanStartCommand()
    {
        return CanStart && activeVehicle.IsOnline;
    }

    [RelayCommand(CanExecute = nameof(CanStartCommand))]
    private async Task StartAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Error = "Connect a vehicle before starting radio calibration.";
            return;
        }

        var accepted = await confirmation.ConfirmAsync(
            "Start radio calibration",
            "Remove propellers and keep the vehicle disarmed. Turn on your transmitter, then move every stick and switch to its full travel.",
            "Start calibration");
        if (!accepted)
        {
            return;
        }

        Error = null;
        try
        {
            await radioService.StartAsync(vehicleId, activeVehicle.ConnectionCancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Starting radio calibration failed for {VehicleId}.", vehicleId);
            Error = exception.Message;
        }
    }

    private bool CanFinishCommand()
    {
        return CanFinish;
    }

    [RelayCommand(CanExecute = nameof(CanFinishCommand))]
    private async Task FinishAsync()
    {
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        try
        {
            var result = await radioService.CompleteAsync(operationCancellation.Token);
            if (!result.Success)
            {
                Error = result.Message;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Finishing radio calibration failed.");
            Error = exception.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanFinishCommand))]
    private async Task CancelCalibrationAsync()
    {
        try
        {
            await radioService.CancelAsync();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Cancelling radio calibration failed.");
            Error = exception.Message;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        radioService.Reset();
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        dispatcher.Dispatch(RefreshLiveChannels);
    }

    private void OnCalibrationStateChanged(object? sender, RadioCalibrationStateChangedEventArgs args)
    {
        dispatcher.Dispatch(() => Show(args.Snapshot));
    }

    private void RefreshLiveChannels()
    {
        if (activeVehicle.VehicleId is not { } vehicleId)
        {
            Channels.Clear();
            OnPropertyChanged(nameof(HasChannels));
            return;
        }

        var view = radioService.GetLiveChannels(vehicleId);
        IsStale = view.IsStale;
        if (Channels.Count == view.Channels.Count &&
            Channels.Zip(view.Channels).All(pair => pair.First.Number == pair.Second.Number))
        {
            for (var index = 0; index < view.Channels.Count; index++)
            {
                Channels[index].Update(view.Channels[index], view.IsStale);
            }
        }
        else
        {
            Channels.Clear();
            foreach (var channel in view.Channels)
            {
                Channels.Add(new RadioChannelDisplayViewModel(channel, view.IsStale));
            }
        }

        Issues.Clear();
        foreach (var issue in view.Issues)
        {
            Issues.Add($"[{issue.Severity}] {issue.Message}");
        }

        OnPropertyChanged(nameof(HasChannels));
        OnPropertyChanged(nameof(HasIssues));
    }

    private void Show(RadioCalibrationSnapshot snapshot)
    {
        CalibrationState = snapshot.State;
        Instruction = snapshot.Instruction;
        Error = snapshot.FailureReason;
        CaptureSummary = snapshot.Captures.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, snapshot.Captures.Select(capture =>
                $"Ch {capture.Number}: {capture.Minimum}-{capture.Maximum} us (range {capture.Range})"));
        if (snapshot.Issues.Count > 0 && snapshot.State is RadioCalibrationState.Failed or RadioCalibrationState.Success or RadioCalibrationState.Writing)
        {
            Issues.Clear();
            foreach (var issue in snapshot.Issues)
            {
                Issues.Add($"[{issue.Severity}] {issue.Message}");
            }

            OnPropertyChanged(nameof(HasIssues));
        }

        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanFinish));
        StartCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
        CancelCalibrationCommand.NotifyCanExecuteChanged();

        if (snapshot.State == RadioCalibrationState.Success && snapshot.VehicleId is { } vehicleId &&
            activeVehicle.State is { } state && state.VehicleId == vehicleId)
        {
            completionStore.Save(workflowCatalog.CreateEvidence(
                SetupWorkflowKey.Radio, state, parameterRegistry.GetAllParameters(vehicleId), clock.UtcNow));
            logger.LogInformation("Recorded confirmed radio setup evidence for {VehicleId}.", vehicleId);
        }
    }
}

/// <summary>Presents one live RC channel with an updating PWM and normalized position.</summary>
public sealed partial class RadioChannelDisplayViewModel : ObservableObject
{
    /// <summary>Initializes a live channel row.</summary>
    /// <param name="info">The channel projection.</param>
    /// <param name="stale">Whether the channel telemetry is stale.</param>
    public RadioChannelDisplayViewModel(RadioChannelInfo info, bool stale)
    {
        Number = info.Number;
        FunctionName = info.FunctionName;
        Update(info, stale);
    }

    /// <summary>Gets the one-based channel number.</summary>
    public int Number { get; }

    /// <summary>Gets the mapped pilot function, when known.</summary>
    public string? FunctionName { get; private set; }

    /// <summary>Gets the channel title.</summary>
    public string Title => FunctionName is null ? $"Channel {Number}" : $"Channel {Number} ({FunctionName})";

    /// <summary>Gets the latest PWM value in microseconds.</summary>
    [ObservableProperty]
    public partial int Pwm { get; private set; }

    /// <summary>Gets the normalized stick position from minus one to one.</summary>
    [ObservableProperty]
    public partial double Normalized { get; private set; }

    /// <summary>Gets the endpoint and reversal summary.</summary>
    [ObservableProperty]
    public partial string Range { get; private set; } = string.Empty;

    /// <summary>Gets whether the channel telemetry is stale.</summary>
    [ObservableProperty]
    public partial bool IsStale { get; private set; }

    /// <summary>Updates the live values from a new projection.</summary>
    /// <param name="info">The channel projection.</param>
    /// <param name="stale">Whether the channel telemetry is stale.</param>
    public void Update(RadioChannelInfo info, bool stale)
    {
        FunctionName = info.FunctionName;
        Pwm = info.Pwm;
        Normalized = info.Normalized;
        IsStale = stale;
        Range = $"{info.Minimum}/{info.Trim}/{info.Maximum}{(info.Reversed ? " · reversed" : string.Empty)}";
    }
}
