using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Commands;
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
public sealed partial class RadioSetupViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IRadioCalibrationService radioService;
    private readonly IDomainEventHub domainEventHub;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly ISetupCompletionStore completionStore;
    private readonly ISetupWorkflowCatalog workflowCatalog;
    private readonly IUserConfirmationService confirmation;
    private readonly IDateTimeProvider clock;
    private readonly IVehicleCommandService commands;
    private readonly IVehicleTelemetryEventHub telemetry;
    private readonly RadioArmingConfiguration armingConfiguration;
    private CancellationTokenSource? bindCancellation;
    private CancellationTokenSource? operationCancellation;
    private IDisposable? vehicleStateSubscription;
    private DateTimeOffset? observedRadioAt;
    private (bool Armed, bool Online)? observedBindSafety;
    private IReadOnlyList<RadioValidationIssue> liveIssues = [];
    private IReadOnlyList<RadioValidationIssue> calibrationIssues = [];
    private readonly INavigationService navigation;

    /// <summary>Initializes the radio Setup workflow.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="radioService">The radio calibration service.</param>
    /// <param name="domainEventHub">The domain event hub used for live radio state.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="completionStore">The Setup evidence store.</param>
    /// <param name="workflowCatalog">The Setup workflow catalog.</param>
    /// <param name="confirmation">The shared confirmation service.</param>
    /// <param name="navigation"> </param>
    /// <param name="clock">The application clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="commands">Safety-gated receiver commands.</param>
    /// <param name="telemetry">Shared diagnostic event hub.</param>
    /// <param name="dispatcher">UI-thread dispatcher.</param>
    /// <param name="armingConfiguration">Guarded RC arming configuration.</param>
    public RadioSetupViewModel(
        IActiveVehicleContext activeVehicle,
        IRadioCalibrationService radioService,
        IDomainEventHub domainEventHub,
        IVehicleParameterRegistry parameterRegistry,
        ISetupCompletionStore completionStore,
        ISetupWorkflowCatalog workflowCatalog,
        IUserConfirmationService confirmation,
        INavigationService navigation,
        IDateTimeProvider clock,
        IVehicleCommandService commands, IVehicleTelemetryEventHub telemetry,
        Utilities.Dispatching.IUiDispatcher dispatcher,
        RadioArmingConfiguration armingConfiguration, ILogger<RadioSetupViewModel> logger)
        : base(logger, dispatcher, domainEventHub)
    {
        this.activeVehicle = activeVehicle;
        this.radioService = radioService;
        this.domainEventHub = domainEventHub;
        this.parameterRegistry = parameterRegistry;
        this.completionStore = completionStore;
        this.workflowCatalog = workflowCatalog;
        this.confirmation = confirmation;
        this.navigation = navigation;
        this.clock = clock;
        this.commands = commands;
        this.telemetry = telemetry;
        this.armingConfiguration = armingConfiguration;
    }

    /// <summary>Gets the live RC channels.</summary>
    public ObservableRangeCollection<RadioChannelDisplayViewModel> Channels
    {
        get;
    } = [];

    /// <summary>Gets the current configuration and validation issues.</summary>
    public ObservableRangeCollection<string> Issues
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

    /// <summary>
    /// 
    /// </summary>
    public void Cancel()
    {
        if (CanCancelCalibration)
        {
            radioService.CancelAsync().SafeFireAndForget();
        }

        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        radioService.StateChanged += OnCalibrationStateChanged;
        activeVehicle.Changed += OnActiveVehicleChanged;
        parameterRegistry.Changed += OnBindParametersChanged;
        observedRadioAt = activeVehicle.State?.Radio.ObservedAt;
        vehicleStateSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated);
        Show(radioService.Current);
        RefreshLiveChannels();

        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        switchMovement.Clear();
        CancelLocalOperation();
        await radioService.CancelAsync();
        radioService.StateChanged -= OnCalibrationStateChanged;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        parameterRegistry.Changed -= OnBindParametersChanged;
        vehicleStateSubscription?.Dispose();
        vehicleStateSubscription = null;
        observedRadioAt = null;
        await base.DeactivateAsync();
    }

    private void CancelLocalOperation()
    {
        bindCancellation?.Cancel();
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        CancelLocalOperation();
        radioService.Dispose();
        base.Dispose();
    }

    private bool CanStartCommand()
    {
        return CanStart && activeVehicle.IsOnline && bindCancellation is null;
    }

    [RelayCommand(CanExecute = nameof(CanStartCommand))]
    private async Task StartAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            SetMessages(null, "Connect a vehicle before starting radio calibration.");
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

        SetMessages(null, null);
        try
        {
            await radioService.StartAsync(vehicleId, activeVehicle.ConnectionCancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogError(exception, "Starting radio calibration failed for {VehicleId}.", vehicleId);
            SetMessages(exception);
        }
    }

    [RelayCommand(CanExecute = nameof(CanFinishCapture))]
    private async Task FinishCaptureAsync()
    {
        try
        {
            await radioService.FinishCaptureAsync(activeVehicle.ConnectionCancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogError(exception, "Finishing radio endpoint capture failed.");
            SetMessages(exception);
        }
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        RefreshLiveChannels();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenFullParametersAsync()
    {
        if (IsBusy)
        {
            return;
        }
        await navigation.NavigateAsync(MissionPlannerRoutes.Configuration);
    }

    [RelayCommand(CanExecute = nameof(CanWrite))]
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
                SetMessages(null, result.Message);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogError(exception, "Finishing radio calibration failed.");
            SetMessages(exception);
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelCalibration))]
    private async Task CancelCalibrationAsync()
    {
        try
        {
            await radioService.CancelAsync();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogError(exception, "Cancelling radio calibration failed.");
            SetMessages(exception);
        }
    }


    [RelayCommand]
    private void Reset()
    {
        try
        {
            radioService.Reset();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Logger.LogError(exception, "Resetting radio calibration failed.");
            SetMessages(exception);
        }
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        if (SetupVehicleChange.IsConnectionOrIdentityBoundary(args))
        {
            Dispatcher.Dispatch(switchMovement.Clear);
        }
        bindCancellation?.Cancel();
        observedRadioAt = args.Current.State?.Radio.ObservedAt;
        Dispatcher.Dispatch(RefreshLiveChannels);
    }

    private Task OnVehicleStateUpdated(VehicleStateUpdated evt, CancellationToken cancellationToken)
    {
        var safety = (evt.VehicleState.IsArmed, evt.VehicleState.ConnectionState == MissionPlanner.Shared.Models.Vehicles.Models.VehicleConnectionState.Online);
        if (evt.VehicleId == activeVehicle.VehicleId && observedBindSafety != safety)
        {
            observedBindSafety = safety;
            Dispatcher.Dispatch(() =>
            {
                OnPropertyChanged(nameof(BindAvailability));
                BindReceiverCommand.NotifyCanExecuteChanged();
            });
        }
        if ((evt.VehicleId == activeVehicle.VehicleId && evt.VehicleState.Radio.ObservedAt != observedRadioAt) || observedRadioAt == null)
        {
            Dispatcher.Dispatch(() =>
            {
                if ((evt.VehicleId == activeVehicle.VehicleId && evt.VehicleState.Radio.ObservedAt != observedRadioAt) || observedRadioAt == null)
                {
                    observedRadioAt = evt.VehicleState.Radio.ObservedAt;
                    RefreshLiveChannels();
                }
            });
        }

        return Task.CompletedTask;
    }

    private void OnCalibrationStateChanged(RadioCalibrationStateChangedEventArgs args)
    {
        Dispatcher.Dispatch(() => Show(args.Snapshot));
    }

    private void RefreshLiveChannels()
    {
        RefreshArmingSwitch();
        OnPropertyChanged(nameof(BindAvailability));
        BindReceiverCommand.NotifyCanExecuteChanged();
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

        liveIssues = view.Issues;
        RefreshIssues();

        OnPropertyChanged(nameof(HasChannels));
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(CanStart));
        StartCommand.NotifyCanExecuteChanged();
    }

    private void Show(RadioCalibrationSnapshot snapshot)
    {
        CalibrationState = snapshot.State;
        Instruction = snapshot.Instruction;
        SetMessages(snapshot.FailureReason ?? snapshot.Instruction, snapshot.FailureReason);
        CaptureSummary = snapshot.Captures.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, snapshot.Captures.Select(capture =>
                $"Ch {capture.Number}: {capture.Minimum}-{capture.Maximum} us (range {capture.Range})"));
        var captures = snapshot.Captures.ToDictionary(capture => capture.Number);
        foreach (var channel in Channels)
        {
            channel.ApplyCalibration(captures.GetValueOrDefault(channel.Number), snapshot.State);
        }

        calibrationIssues = snapshot.Issues;
        RefreshIssues();

        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanFinishCapture));
        OnPropertyChanged(nameof(CanWrite));
        OnPropertyChanged(nameof(CanCancelCalibration));
        OnPropertyChanged(nameof(IsWriting));
        StartCommand.NotifyCanExecuteChanged();
        FinishCaptureCommand.NotifyCanExecuteChanged();
        ConfirmAndWriteCommand.NotifyCanExecuteChanged();
        CancelCalibrationCommand.NotifyCanExecuteChanged();
        BindReceiverCommand.NotifyCanExecuteChanged();

        if (snapshot.State == RadioCalibrationState.Success && snapshot.VehicleId is { } vehicleId &&
            activeVehicle.State is { } state && state.VehicleId == vehicleId)
        {
            completionStore.Save(workflowCatalog.CreateEvidence(
                SetupWorkflowKey.Radio, state, parameterRegistry.GetAllParameters(vehicleId), clock.UtcNow));
            Logger.LogInformation("Recorded confirmed radio setup evidence for {VehicleId}.", vehicleId);
        }
    }

    private void RefreshIssues()
    {
        var messages = calibrationIssues.Concat(liveIssues)
            .Select(issue => $"[{issue.Severity}] {issue.Message}").Distinct().ToArray();
        if (!Issues.SequenceEqual(messages))
        {
            Issues.ReplaceRange(messages);
        }
        OnPropertyChanged(nameof(HasIssues));
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

    /// <summary>Gets the current neutral/trim assessment for a centered pilot input.</summary>
    [ObservableProperty]
    public partial string? NeutralDiagnosticText
    {
        get; private set;
    }

    /// <summary>Gets whether the neutral assessment needs user attention.</summary>
    [ObservableProperty]
    public partial bool HasNeutralWarning
    {
        get; private set;
    }

    /// <summary>Gets the channel's calibration role and assignment evidence.</summary>
    [ObservableProperty]
    public partial string Classification { get; private set; } = string.Empty;

    /// <summary>Gets explicitly observed values, separate from stored parameters.</summary>
    [ObservableProperty]
    public partial string ObservedValues { get; private set; } = "Observed MIN / center / MAX: not captured";

    /// <summary>Gets the per-channel validation result.</summary>
    [ObservableProperty]
    public partial string ValidationStatus { get; private set; } = "Not captured";

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
        Classification = info.FunctionName is null
            ? "Unused / unassigned — ignored"
            : info.Kind is RadioChannelKind.CenteredAxis or RadioChannelKind.Throttle
                ? $"Required primary control — {info.FunctionName}"
                : $"Used auxiliary channel — {info.FunctionName}";
        IsReversed = info.Reversed;
        PresentationKind = info.Kind switch
        {
            RadioChannelKind.CenteredAxis => RadioChannelPresentationKind.CenteredAxis,
            RadioChannelKind.Throttle => RadioChannelPresentationKind.Throttle,
            var _ => RadioChannelPresentationKind.Auxiliary
        };
        var diagnostic = info.NeutralDiagnostic;
        HasNeutralWarning = diagnostic?.NeutralAllowed == false || diagnostic?.CenterOutsideDeadZone == true;
        NeutralDiagnosticText = diagnostic is null
            ? null
            : $"Neutral: {(diagnostic.NeutralAllowed is null ? "unknown (stale input or missing trim/dead-zone)" : diagnostic.NeutralAllowed.Value ? "Neutral" : "Not neutral")} · " +
              $"Center error: {(diagnostic.CenterError is { } error ? error.ToString("+0;-0;0") + " µs" : "awaiting neutral review")} · " +
              $"Asymmetry: {(diagnostic.Asymmetry is { } asymmetry ? asymmetry + " µs" : "not captured")}" +
              (HasNeutralWarning
                  ? " — Check transmitter trim/subtrim, mixer/input/output offset, stick calibration, or stale RCx_TRIM. Review before changing any parameter."
                  : string.Empty);
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
        ObservedValues = capture is null
            ? "Observed MIN / center / MAX: not captured"
            : $"Observed MIN / center / MAX: {capture.Minimum} / {capture.CandidateTrim?.ToString() ?? "awaiting neutral review"} / {capture.Maximum} µs";
        ValidationStatus = FunctionName is null
            ? "Ignored — no assigned function; no parameters will be written"
            : CalibrationIssue is not null
                ? "Validation failed — see details below"
                : state is RadioCalibrationState.Review or RadioCalibrationState.Success
                    ? "Endpoint validation passed"
                    : "Awaiting endpoint validation";
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

