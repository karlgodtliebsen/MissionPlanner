using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.Common;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Projects live RC channels and the radio endpoint-calibration state machine into Setup controls.</summary>
public sealed partial class RadioSetupViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IRadioCalibrationService radioService;
    private readonly IDomainEventHub domainEventHub;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly ISetupCompletionStore completionStore;
    private readonly ISetupWorkflowCatalog workflowCatalog;
    private readonly IUserConfirmationService confirmation;
    private readonly IDateTimeProvider clock;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<RadioSetupViewModel> logger;
    private CancellationTokenSource? operationCancellation;
    private IDisposable? vehicleStateSubscription;
    private DateTimeOffset? observedRadioAt;

    /// <summary>Initializes the radio Setup workflow.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="radioService">The radio calibration service.</param>
    /// <param name="domainEventHub">The domain event hub used for live radio state.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="completionStore">The Setup evidence store.</param>
    /// <param name="workflowCatalog">The Setup workflow catalog.</param>
    /// <param name="confirmation">The shared confirmation service.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="dispatcher">The UI Dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public RadioSetupViewModel(
        IActiveVehicleContext activeVehicle,
        IRadioCalibrationService radioService,
        IDomainEventHub domainEventHub,
        IVehicleParameterRegistry parameterRegistry,
        ISetupCompletionStore completionStore,
        ISetupWorkflowCatalog workflowCatalog,
        IUserConfirmationService confirmation,
        IDateTimeProvider clock,
        IDispatcher dispatcher,
        ILogger<RadioSetupViewModel> logger)
        : base(workflowCatalog.Workflows.First(w => w.Key == SetupWorkflowKey.Radio))
    {
        this.activeVehicle = activeVehicle;
        this.radioService = radioService;
        this.domainEventHub = domainEventHub;
        this.parameterRegistry = parameterRegistry;
        this.completionStore = completionStore;
        this.workflowCatalog = workflowCatalog;
        this.confirmation = confirmation;
        this.clock = clock;
        this.dispatcher = dispatcher;
        this.logger = logger;
        radioService.StateChanged += OnCalibrationStateChanged;
        activeVehicle.Changed += OnActiveVehicleChanged;
        observedRadioAt = activeVehicle.State?.Radio.ObservedAt;
        vehicleStateSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated);
        Show(radioService.Current);
        RefreshLiveChannels();
    }

    /// <summary>Gets the live RC channels.</summary>
    public ObservableCollection<RadioChannelDisplayViewModel> Channels
    {
        get;
    } = [];

    /// <summary>Gets the current configuration and validation issues.</summary>
    public ObservableCollection<string> Issues
    {
        get;
    } = [];

    /// <summary>Gets whether the RC telemetry is stale.</summary>
    [ObservableProperty]
    public partial bool IsStale
    {
        get;
        private set;
    } = true;

    /// <summary>Gets the receiver signal state.</summary>
    [ObservableProperty]
    public partial RadioSignalState SignalState
    {
        get;
        private set;
    } = RadioSignalState.NoSignal;

    /// <summary>Gets a concise receiver state label.</summary>
    [ObservableProperty]
    public partial string SignalStatus
    {
        get;
        private set;
    } = "No signal";

    /// <summary>Gets the number of currently observed RC channels.</summary>
    [ObservableProperty]
    public partial int ChannelCount
    {
        get;
        private set;
    }

    /// <summary>Gets RC input RSSI text, or an explicit unavailable marker.</summary>
    [ObservableProperty]
    public partial string RssiText
    {
        get;
        private set;
    } = "RSSI —";

    /// <summary>Gets the resolved pilot-channel map summary.</summary>
    [ObservableProperty]
    public partial string ChannelMapSummary
    {
        get;
        private set;
    } = "Map unavailable";

    /// <summary>Gets whether the connected vehicle is armed.</summary>
    [ObservableProperty]
    public partial bool IsArmed
    {
        get;
        private set;
    }

    /// <summary>Gets the vehicle safety-state label.</summary>
    public string VehicleSafetyStatus => IsArmed ? "ARMED — writing blocked" : "Disarmed";

    /// <summary>Gets the current calibration workflow stage.</summary>
    [ObservableProperty]
    public partial RadioCalibrationState CalibrationState
    {
        get;
        private set;
    }

    /// <summary>Gets the primary calibration instruction.</summary>
    [ObservableProperty]
    public partial string Instruction
    {
        get;
        private set;
    } = string.Empty;

    /// <summary>Gets a summary of captured endpoints during calibration.</summary>
    [ObservableProperty]
    public partial string CaptureSummary
    {
        get;
        private set;
    } = string.Empty;

    /// <summary>Gets whether any live channels are available.</summary>
    public bool HasChannels => Channels.Count > 0;

    /// <summary>Gets whether any configuration or validation issues exist.</summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>Gets whether calibration capture can start.</summary>
    public bool CanStart =>
        CalibrationState is RadioCalibrationState.NotStarted or RadioCalibrationState.Success or
            RadioCalibrationState.Failed or RadioCalibrationState.Cancelled or RadioCalibrationState.Disconnected &&
        SignalState == RadioSignalState.Live && !IsArmed;

    /// <summary>Gets whether parameter writes are currently in progress.</summary>
    public bool IsWriting => CalibrationState == RadioCalibrationState.Writing;

    /// <summary>Gets whether endpoint capture can finish and enter Review.</summary>
    public bool CanFinishCapture => CalibrationState == RadioCalibrationState.Capturing;

    /// <summary>Gets whether reviewed values can be confirmed and written.</summary>
    public bool CanWrite => CalibrationState == RadioCalibrationState.Review;

    /// <summary>Gets whether the active non-destructive workflow can be cancelled.</summary>
    public bool CanCancelCalibration => CalibrationState is RadioCalibrationState.Capturing or RadioCalibrationState.Review;

    /// <inheritdoc />
    public override void Cancel()
    {
        if (CanCancelCalibration)
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
        vehicleStateSubscription?.Dispose();
        vehicleStateSubscription = null;
        observedRadioAt = null;
        base.Dispose();
        radioService.Dispose();
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

    private bool CanFinishCaptureCommand()
    {
        return CanFinishCapture;
    }

    [RelayCommand(CanExecute = nameof(CanFinishCaptureCommand))]
    private async Task FinishCaptureAsync()
    {
        try
        {
            await radioService.FinishCaptureAsync(activeVehicle.ConnectionCancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Finishing radio endpoint capture failed.");
            Error = exception.Message;
        }
    }

    private bool CanWriteCommand()
    {
        return CanWrite;
    }

    [RelayCommand(CanExecute = nameof(CanWriteCommand))]
    private async Task ConfirmAndWriteAsync()
    {
        var accepted = await confirmation.ConfirmAsync(
            "Write radio calibration",
            "Confirm the vehicle is disarmed, centered controls are neutral, and conventional throttle is fully low. The displayed MIN, TRIM, and MAX values will be written and verified.",
            "Write and verify");
        if (!accepted)
        {
            return;
        }

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

    [RelayCommand(CanExecute = nameof(CanCancelCalibrationCommand))]
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

    private bool CanCancelCalibrationCommand()
    {
        return CanCancelCalibration;
    }

    [RelayCommand]
    private void Reset()
    {
        radioService.Reset();
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        observedRadioAt = args.Current.State?.Radio.ObservedAt;
        dispatcher.Dispatch(RefreshLiveChannels);
    }

    private Task OnVehicleStateUpdated(VehicleStateUpdated evt, CancellationToken cancellationToken)
    {
        if (evt.VehicleId == activeVehicle.VehicleId && evt.VehicleState.Radio.ObservedAt != observedRadioAt)
        {
            dispatcher.Dispatch(() =>
            {
                if (evt.VehicleId == activeVehicle.VehicleId &&
                    evt.VehicleState.Radio.ObservedAt != observedRadioAt)
                {
                    observedRadioAt = evt.VehicleState.Radio.ObservedAt;
                    RefreshLiveChannels();
                }
            });
        }

        return Task.CompletedTask;
    }

    private void OnCalibrationStateChanged(object? sender, RadioCalibrationStateChangedEventArgs args)
    {
        dispatcher.Dispatch(() => Show(args.Snapshot));
    }

    private void RefreshLiveChannels()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            Channels.Clear();
            SignalState = RadioSignalState.NoSignal;
            SignalStatus = "No RC signal";
            ChannelCount = 0;
            RssiText = "RSSI —";
            ChannelMapSummary = "Map unavailable";
            IsArmed = false;
            OnPropertyChanged(nameof(VehicleSafetyStatus));
            OnPropertyChanged(nameof(CanStart));
            StartCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasChannels));
            return;
        }

        var view = radioService.GetLiveChannels(vehicleId);
        IsStale = view.IsStale;
        SignalState = view.SignalState;
        SignalStatus = view.SignalState switch
        {
            RadioSignalState.Live => "RC input live",
            RadioSignalState.Stale => "RC input stale",
            var _ => "No RC signal"
        };
        ChannelCount = view.ReportedChannelCount;
        RssiText = view.RssiPercent is { } rssi ? $"RSSI {rssi}%" : "RSSI —";
        ChannelMapSummary = view.ChannelMapSummary;
        IsArmed = view.IsArmed;
        OnPropertyChanged(nameof(VehicleSafetyStatus));
        var captures = radioService.Current.Captures.ToDictionary(capture => capture.Number);
        if (Channels.Count == view.Channels.Count &&
            Channels.Zip(view.Channels).All(pair => pair.First.Number == pair.Second.Number))
        {
            for (var index = 0; index < view.Channels.Count; index++)
            {
                var info = view.Channels[index];
                Channels[index].Update(info, view.IsStale, true, captures.GetValueOrDefault(info.Number), CalibrationState);
            }
        }
        else if (view.Channels.Count == 0 && Channels.Count > 0)
        {
            foreach (var channel in Channels)
            {
                channel.SetSignalState(false, view.IsStale);
            }
        }
        else
        {
            Channels.Clear();
            foreach (var channel in view.Channels)
            {
                Channels.Add(new RadioChannelDisplayViewModel(
                    channel,
                    view.IsStale,
                    captures.GetValueOrDefault(channel.Number),
                    CalibrationState));
            }
        }

        Issues.Clear();
        foreach (var issue in view.Issues)
        {
            Issues.Add($"[{issue.Severity}] {issue.Message}");
        }

        OnPropertyChanged(nameof(HasChannels));
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(CanStart));
        StartCommand.NotifyCanExecuteChanged();
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
        var captures = snapshot.Captures.ToDictionary(capture => capture.Number);
        foreach (var channel in Channels)
        {
            channel.ApplyCalibration(captures.GetValueOrDefault(channel.Number), snapshot.State);
        }

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
        OnPropertyChanged(nameof(CanFinishCapture));
        OnPropertyChanged(nameof(CanWrite));
        OnPropertyChanged(nameof(CanCancelCalibration));
        OnPropertyChanged(nameof(IsWriting));
        StartCommand.NotifyCanExecuteChanged();
        FinishCaptureCommand.NotifyCanExecuteChanged();
        ConfirmAndWriteCommand.NotifyCanExecuteChanged();
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
    /// <param name="capture"></param>
    /// <param name="calibrationState"></param>
    public RadioChannelDisplayViewModel(
        RadioChannelInfo info,
        bool stale,
        RadioChannelCapture? capture = null,
        RadioCalibrationState calibrationState = RadioCalibrationState.NotStarted)
    {
        Number = info.Number;
        Update(info, stale, true, capture, calibrationState);
    }

    /// <summary>Gets the one-based channel number.</summary>
    public int Number
    {
        get;
    }

    /// <summary>Gets the mapped pilot function, when known.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    [NotifyPropertyChangedFor(nameof(RoleLabel))]
    public partial string? FunctionName
    {
        get;
        private set;
    }

    /// <summary>Gets the channel title.</summary>
    public string Title => FunctionName is null ? $"Channel {Number}" : $"Channel {Number} ({FunctionName})";

    /// <summary>Gets the compact mapped role or AUX label.</summary>
    public string RoleLabel => FunctionName?.ToUpperInvariant() ?? "AUX";

    /// <summary>Gets the latest PWM value in microseconds.</summary>
    [ObservableProperty]
    public partial int Pwm
    {
        get;
        private set;
    }

    /// <summary>Gets the normalized stick position from minus one to one.</summary>
    [ObservableProperty]
    public partial double Normalized
    {
        get;
        private set;
    }

    /// <summary>Gets the configured minimum endpoint.</summary>
    [ObservableProperty]
    public partial int Minimum
    {
        get;
        private set;
    }

    /// <summary>Gets the configured maximum endpoint.</summary>
    [ObservableProperty]
    public partial int Maximum
    {
        get;
        private set;
    }

    /// <summary>Gets the configured trim.</summary>
    [ObservableProperty]
    public partial int Trim
    {
        get;
        private set;
    }

    /// <summary>Gets the centered-axis dead zone.</summary>
    [ObservableProperty]
    public partial int DeadZone
    {
        get;
        private set;
    }

    /// <summary>Gets whether the channel is reversed.</summary>
    [ObservableProperty]
    public partial bool IsReversed
    {
        get;
        private set;
    }

    /// <summary>Gets whether a live PWM value is available.</summary>
    [ObservableProperty]
    public partial bool HasSignal
    {
        get;
        private set;
    }

    /// <summary>Gets the meter presentation kind.</summary>
    [ObservableProperty]
    public partial RadioChannelPresentationKind PresentationKind
    {
        get;
        private set;
    }

    /// <summary>Gets the captured minimum endpoint.</summary>
    [ObservableProperty]
    public partial int? CapturedMinimum
    {
        get;
        private set;
    }

    /// <summary>Gets the captured maximum endpoint.</summary>
    [ObservableProperty]
    public partial int? CapturedMaximum
    {
        get;
        private set;
    }

    /// <summary>Gets the fresh Review-stage trim candidate.</summary>
    [ObservableProperty]
    public partial int? CandidateTrim
    {
        get;
        private set;
    }

    /// <summary>Gets whether captured markers should be rendered.</summary>
    [ObservableProperty]
    public partial bool ShowCapturedRange
    {
        get;
        private set;
    }

    /// <summary>Gets an optional honest auxiliary-position interpretation.</summary>
    [ObservableProperty]
    public partial string? AuxiliaryState
    {
        get;
        private set;
    }

    /// <summary>Gets the channel-specific validation message.</summary>
    [ObservableProperty]
    public partial string? CalibrationIssue
    {
        get;
        private set;
    }

    /// <summary>Gets the endpoint and reversal summary.</summary>
    [ObservableProperty]
    public partial string Range
    {
        get;
        private set;
    } = string.Empty;

    /// <summary>Gets whether the channel telemetry is stale.</summary>
    [ObservableProperty]
    public partial bool IsStale
    {
        get;
        private set;
    }

    /// <summary>Updates the live values from a new projection.</summary>
    /// <param name="info">The channel projection.</param>
    /// <param name="stale">Whether the channel telemetry is stale.</param>
    /// <param name="hasSignal"></param>
    /// <param name="capture"></param>
    /// <param name="calibrationState"></param>
    public void Update(
        RadioChannelInfo info,
        bool stale,
        bool hasSignal = true,
        RadioChannelCapture? capture = null,
        RadioCalibrationState calibrationState = RadioCalibrationState.NotStarted)
    {
        FunctionName = info.FunctionName;
        Pwm = info.Pwm;
        Normalized = info.Normalized;
        IsStale = stale;
        HasSignal = hasSignal;
        Minimum = info.Minimum;
        Maximum = info.Maximum;
        Trim = info.Trim;
        DeadZone = info.DeadZone;
        IsReversed = info.Reversed;
        PresentationKind = info.Kind switch
        {
            RadioChannelKind.CenteredAxis => RadioChannelPresentationKind.CenteredAxis,
            RadioChannelKind.Throttle => RadioChannelPresentationKind.Throttle,
            var _ => RadioChannelPresentationKind.Auxiliary
        };
        AuxiliaryState = info.Kind == RadioChannelKind.Auxiliary ? DescribeAuxiliary(info.Pwm) : null;
        Range = $"{info.Minimum}/{info.Trim}/{info.Maximum}{(info.Reversed ? " · reversed" : string.Empty)}";
        ApplyCalibration(capture, calibrationState);
    }

    /// <summary>Updates only signal availability while retaining the last known raw value.</summary>
    public void SetSignalState(bool hasSignal, bool stale)
    {
        HasSignal = hasSignal;
        IsStale = stale;
    }

    /// <summary>Projects structured calibration markers without parsing display strings.</summary>
    public void ApplyCalibration(RadioChannelCapture? capture, RadioCalibrationState state)
    {
        CapturedMinimum = capture?.Minimum;
        CapturedMaximum = capture?.Maximum;
        CandidateTrim = capture?.CandidateTrim;
        ShowCapturedRange = capture is not null && state is RadioCalibrationState.Capturing or RadioCalibrationState.Review or RadioCalibrationState.Writing or RadioCalibrationState.Success or RadioCalibrationState.Failed;
        CalibrationIssue = capture?.Issues.FirstOrDefault()?.Message;
    }

    /// <summary>Returns an optional stepped label without coercing intermediate auxiliary values.</summary>
    public static string DescribeAuxiliary(int pwm)
    {
        return pwm switch
        {
            <= 1100 => "LOW",
            >= 1450 and <= 1550 => "MID",
            >= 1900 => "HIGH",
            var _ => "Variable"
        };
    }
}
