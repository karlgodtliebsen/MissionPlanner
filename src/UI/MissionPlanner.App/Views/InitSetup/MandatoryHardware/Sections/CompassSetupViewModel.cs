using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Presentation.Documents;
using MissionPlanner.App.Utilities.Dispatching;
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

/// <summary>Semantic Compass setup with local reviewable edits and the existing calibration workflow.</summary>
public sealed partial class CompassSetupViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly ICompassConfigurationService compassService;
    private readonly IArduPilotCompassCalibrationService calibration;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly ISetupCompletionStore completionStore;
    private readonly ISetupWorkflowCatalog workflowCatalog;
    private readonly IUserConfirmationService confirmation;
    private readonly IDateTimeProvider clock;
    private readonly IVehicleCommandService commands;
    private readonly INavigationService navigation;
    private readonly IVehicleParameterLoadStatusContext loadStatus;
    private readonly IDomainEventHub domain;
    private CompassSetupState? current;
    private CompassConfiguration? desired;
    private CompassChangeSet? review;
    private MissionPlanner.Firmware.Model.VehicleFirmwareIdentity? firmware;
    private CancellationTokenSource? operationCancellation;
    private IDisposable? loadSubscription;
    private Timer? refreshTimer;
    private bool active;
    private bool loading;
    private int editVersion;
    private int loadVersion;
    private bool rebootRequested;
    private readonly ICompassSetupDocumentFactory documentFactory;
    private CompassDocumentContext? documentContext;

    /// <summary>Creates the semantic page projection and retains calibration and completion services.</summary>
    public CompassSetupViewModel(IActiveVehicleContext activeVehicle, ICompassConfigurationService compassService,
        IArduPilotCompassCalibrationService calibration, IVehicleParameterRegistry parameterRegistry,
        ISetupCompletionStore completionStore, ISetupWorkflowCatalog workflowCatalog,
        IUserConfirmationService confirmation, IDateTimeProvider clock, ILogger<CompassSetupViewModel> logger,
        IUiDispatcher dispatcher, IDomainEventHub domain, IVehicleParameterLoadStatusContext loadStatus,
        IVehicleCommandService commands, INavigationService navigation,
        ICompassSetupDocumentFactory documentFactory) : base(logger, dispatcher, domain)
    {
        this.activeVehicle = activeVehicle;
        this.compassService = compassService;
        this.calibration = calibration;
        this.parameterRegistry = parameterRegistry;
        this.completionStore = completionStore;
        this.workflowCatalog = workflowCatalog;
        this.confirmation = confirmation;
        this.clock = clock;
        this.commands = commands;
        this.navigation = navigation;
        this.loadStatus = loadStatus;
        this.domain = domain;
        this.documentFactory = documentFactory;
        UpdateDocument();
    }

    /// <summary>Selectable application explanation of current and pending Compass state.</summary>
    [ObservableProperty]
    public partial UserDocument? StatusDocument
    {
        get; private set;
    }

    /// <summary>Selectable advanced evidence with editor-compatible parameter assignments.</summary>
    [ObservableProperty]
    public partial UserDocument? DiagnosticDocument
    {
        get; private set;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (documentFactory is not null && e.PropertyName is nameof(StatusMessage) or nameof(ErrorMessage))
        {
            UpdateDocument();
        }
    }

    private void UpdateDocument()
    {
        var next = new CompassDocumentContext(current, desired, activeVehicle.IsOnline,
            IsLoadingParameters || current is null, ValidationText, CalibrationState,
            Instruction, ProgressSummary, QualitySummary, RequiresReboot, StatusMessage, ErrorMessage);
        var statusChanged = documentContext?.HasSameContent(next) != true;
        if (statusChanged)
        {
            var document = documentFactory.Create(next);
            if (StatusDocument?.Markdown != document.Markdown)
            {
                StatusDocument = document;
            }
        }
        if (statusChanged || !(documentContext?.State?.Diagnostics ?? []).SequenceEqual(next.State?.Diagnostics ?? []))
        {
            var document = documentFactory.CreateDiagnostics(next);
            if (DiagnosticDocument?.Markdown != document.Markdown)
            {
                DiagnosticDocument = document;
            }
        }
        documentContext = next;
    }

    /// <summary>Semantic configuration editors.</summary>
    public ObservableRangeCollection<CompassSettingViewModel> Settings { get; } = [];
    /// <summary>Read-only detected device identities.</summary>
    public ObservableRangeCollection<string> Devices { get; } = [];
    /// <summary>Explicit pending mutations including dependencies.</summary>
    public ObservableRangeCollection<string> PendingChanges { get; } = [];

    /// <summary>Current subsystem status.</summary>
    [ObservableProperty] public partial string StatusText { get; private set; } = "Connect a vehicle.";
    /// <summary>Configuration validity, distinct from arming evidence.</summary>
    [ObservableProperty] public partial string ValidationText { get; private set; } = "Compass capability unknown.";
    /// <summary>Current telemetry-based arming explanation.</summary>
    [ObservableProperty] public partial string ArmingImpact { get; private set; } = "Arming impact unknown.";
    /// <summary>Current EKF yaw source.</summary>
    [ObservableProperty] public partial string YawSourceText { get; private set; } = "Unknown";
    /// <summary>Whether staged values differ from current values.</summary>
    [ObservableProperty]
    public partial bool HasPendingChanges
    {
        get; private set;
    }
    /// <summary>Whether live values changed underneath local edits.</summary>
    [ObservableProperty]
    public partial bool HasConflict
    {
        get; private set;
    }
    /// <summary>Whether a confirmed mutation still requires a vehicle reboot.</summary>
    [ObservableProperty]
    public partial bool RequiresReboot
    {
        get; private set;
    }
    /// <summary>Review count and reboot notice.</summary>
    [ObservableProperty] public partial string PendingSummary { get; private set; } = string.Empty;
    /// <summary>Calibration state projected from the existing service.</summary>
    [ObservableProperty]
    public partial CompassCalibrationWorkflowState CalibrationState
    {
        get; private set;
    }
    /// <summary>Calibration instructions.</summary>
    [ObservableProperty] public partial string Instruction { get; private set; } = string.Empty;
    /// <summary>Per-device calibration progress.</summary>
    [ObservableProperty] public partial string ProgressSummary { get; private set; } = string.Empty;
    /// <summary>Calibration quality evidence.</summary>
    [ObservableProperty]
    public partial string? QualitySummary
    {
        get; private set;
    }
    /// <summary>Whether configuration controls can currently be edited.</summary>
    public bool CanEdit => activeVehicle.IsOnline && current?.IsSupported == true && !IsBusy && !CanCancel && !IsLoadingParameters;
    /// <summary>Whether calibration may start with confirmed enabled configuration.</summary>
    public bool CanStart => CanEdit && current?.Current.Enabled == true && !HasPendingChanges && !HasConflict &&
        activeVehicle.State?.IsArmed == false && !RequiresReboot && CalibrationState is
        CompassCalibrationWorkflowState.NotStarted or CompassCalibrationWorkflowState.Success or CompassCalibrationWorkflowState.Failed or
        CompassCalibrationWorkflowState.Cancelled or CompassCalibrationWorkflowState.Disconnected;
    /// <summary>Whether calibration can accept current results.</summary>
    public bool CanAccept => CalibrationState == CompassCalibrationWorkflowState.PendingAcceptance && activeVehicle.IsOnline;
    /// <summary>Whether an active calibration can be cancelled.</summary>
    public bool CanCancel => CalibrationState is CompassCalibrationWorkflowState.Preparing or CompassCalibrationWorkflowState.Running or CompassCalibrationWorkflowState.PendingAcceptance;
    /// <summary>Why calibration is unavailable.</summary>
    public string CalibrationAvailability => current?.Current.Enabled != true ? "Enable compass and apply before calibration." :
        RequiresReboot ? "Reboot after applying configuration before calibration." : HasPendingChanges ? "Apply or discard configuration changes before calibration." :
        !activeVehicle.IsOnline ? "Reconnect before calibration." : "Move away from metal and magnetic interference. Calibration results require explicit acceptance.";
    /// <summary>Whether a fresh valid review can be applied.</summary>
    public bool CanApply => CanEdit && !HasConflict && activeVehicle.State?.IsArmed == false && review?.CanApply == true;
    /// <summary>Whether safe reboot is available.</summary>
    public bool CanReboot => RequiresReboot && activeVehicle.IsOnline && activeVehicle.State?.IsArmed == false && !IsBusy && !CanCancel && !HasPendingChanges;
    private bool IsLoadingParameters => activeVehicle.VehicleId is { } id && loadStatus.Get(id)?.IsInProgress == true;

    /// <summary>Reads current semantic state without replacing pending edits.</summary>
    public async Task LoadAsync()
    {
        if (loading || IsBusy)
        {
            return;
        }
        if (activeVehicle.VehicleId is not { } id || !activeVehicle.IsOnline)
        {
            StatusText = "Vehicle disconnected. Pending edits are local; Apply is disabled.";
            ArmingImpact = "Arming impact unknown while disconnected.";
            NotifyAvailability();
            return;
        }
        if (IsLoadingParameters)
        {
            StatusText = loadStatus.Get(id)!.Message;
            NotifyAvailability();
            return;
        }
        loading = true;
        var generation = loadVersion;
        var token = activeVehicle.ConnectionCancellationToken;
        try
        {
            var state = await compassService.ReadAsync(id, token);
            token.ThrowIfCancellationRequested();
            await Dispatcher.DispatchAsync(() =>
            {
                if (generation != loadVersion || activeVehicle.VehicleId != id || activeVehicle.ConnectionCancellationToken != token)
                {
                    return;
                }
                if (current?.VehicleId != id || firmware != activeVehicle.State?.Identity.Firmware)
                {
                    desired = null;
                    review = null;
                    HasPendingChanges = false;
                    PendingChanges.Clear();
                    HasConflict = false;
                    RequiresReboot = false;
                }
                firmware = activeVehicle.State?.Identity.Firmware;
                var changed = current?.Current != state.Current;
                var capabilitiesChanged = current is null || state.Settings.Any(setting =>
                {
                    var old = current.Settings.FirstOrDefault(s => s.Setting == setting.Setting);
                    return old is null || old.CanEdit != setting.CanEdit || old.Default != setting.Default || !old.Choices.SequenceEqual(setting.Choices);
                });
                if (HasPendingChanges && changed)
                {
                    HasConflict = true;
                }
                current = state;
                if (!HasPendingChanges || desired is null)
                {
                    desired = state.Current;
                }
                ProjectState(state);
                if (changed || capabilitiesChanged || Settings.Count == 0)
                {
                    ProjectSettings();
                }
                NotifyAvailability();
                if (capabilitiesChanged && HasPendingChanges)
                {
                    review = null;
                    _ = EvaluateAsync(++editVersion);
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
        finally
        {
            loading = false;
        }
    }

    private void ProjectState(CompassSetupState state)
    {
        StatusText = state.IsSupported ? $"Compass: {(state.Current.Enabled is null ? "Unknown" : state.Current.Enabled.Value ? "Enabled" : "Disabled")} · Health: {state.Health}" : state.UnsupportedReason!;
        if (HasConflict || !HasPendingChanges)
        {
            ValidationText = HasConflict ? "Live values changed while editing. Discard and review the current configuration before applying." : state.Validation;
        }
        ArmingImpact = state.ArmingImpact;
        var yaw = state.Settings.FirstOrDefault(s => s.Setting == CompassSetting.YawSource);
        YawSourceText = yaw?.Choices.FirstOrDefault(c => c.Value == yaw.Current)?.Label ?? "Unknown or unrecognized";
        Devices.ReplaceRange(state.DetectedDevices);
    }

    private void ProjectSettings()
    {
        Settings.ReplaceRange(current?.Settings.Select(definition => new CompassSettingViewModel(definition, desired?.Get(definition.Setting), OnEdited)) ?? []);
    }

    private void OnEdited()
    {
        if (current is null || desired is null)
        {
            return;
        }
        foreach (var setting in Settings.Where(setting => setting.Selected is not null))
        {
            desired = desired.With(setting.Definition.Setting, setting.Selected!.Value);
        }
        HasPendingChanges = desired != current.Current;
        review = null;
        NotifyAvailability();
        _ = EvaluateAsync(++editVersion);
    }

    private async Task EvaluateAsync(int version)
    {
        if (current is null || desired is null || !activeVehicle.IsOnline)
        {
            return;
        }
        var id = current.VehicleId;
        var token = activeVehicle.ConnectionCancellationToken;
        try
        {
            var changes = await compassService.EvaluateChangesAsync(id, desired, token);
            await Dispatcher.DispatchAsync(() =>
            {
                if (version != editVersion || token != activeVehicle.ConnectionCancellationToken || token.IsCancellationRequested)
                {
                    return;
                }
                if (current.Current != changes.Original)
                {
                    HasConflict = true;
                }
                review = changes;
                desired = changes.Desired;
                HasPendingChanges = desired != current.Current;
                PendingChanges.ReplaceRange(changes.Changes.Select(c => $"{c.Reason}: {c.Name} {c.OldValue} → {c.NewValue}"));
                PendingSummary = $"{changes.Changes.Count} unsaved changes" + (changes.RequiresReboot ? " · Reboot required after applying." : string.Empty);
                ValidationText = HasConflict ? "Live values changed while editing. Discard and review again." :
                    changes.Errors.Count > 0 ? string.Join(Environment.NewLine, changes.Errors) :
                    changes.Warnings.Count > 0 ? string.Join(Environment.NewLine, changes.Warnings) : current.Validation;
                ProjectSettings();
                NotifyAvailability();
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
    }

    [RelayCommand]
    private void Discard()
    {
        editVersion++;
        desired = current?.Current;
        review = null;
        HasPendingChanges = false;
        HasConflict = false;
        PendingChanges.Clear();
        ProjectSettings();
        if (current is not null)
        {
            ProjectState(current);
        }
        NotifyAvailability();
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        if (!CanApply || review is not { } captured)
        {
            return;
        }
        var accepted = await confirmation.ConfirmAsync("Apply compass configuration",
            string.Join(Environment.NewLine, PendingChanges) + (captured.RequiresReboot ? "\nReboot required after applying." : string.Empty), "Apply and verify");
        if (!accepted || !CanApply || review != captured)
        {
            return;
        }
        IsBusy = true;
        NotifyAvailability();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(captured.Connection);
        try
        {
            var result = await compassService.ApplyAsync(captured.Scope.VehicleId, captured, operationCancellation.Token);
            RequiresReboot |= result.RequiresReboot;
            if (result.Actual is not null)
            {
                current = result.Actual;
                ProjectState(current);
            }
            if (result.Success)
            {
                Discard();
            }
            else
            {
                await EvaluateAsync(++editVersion);
            }
            SetMessages(result.Message + (RequiresReboot ? " Reboot required." : string.Empty));
        }
        catch (OperationCanceledException)
        {
            SetMessages("Apply interrupted. Refresh actual values and review before retrying.");
            HasConflict = true;
        }
        catch (Exception exception)
        {
            SetMessages(exception);
            HasConflict = true;
        }
        finally
        {
            operationCancellation?.Dispose();
            operationCancellation = null;
            IsBusy = false;
            NotifyAvailability();
        }
    }

    [RelayCommand]
    private async Task RefreshInventoryAsync()
    {
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId is not { } id || IsBusy)
        {
            return;
        }
        try
        {
            await compassService.RefreshAsync(id, activeVehicle.ConnectionCancellationToken);
            await LoadAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
    }

    [RelayCommand]
    private async Task OpenFullParametersAsync()
    {
        if (HasPendingChanges && !await confirmation.ConfirmAsync("Leave Compass setup", "Discard local compass edits and open Parameters Editor?", "Discard and open"))
        {
            return;
        }
        Discard();
        await navigation.NavigateAsync(MissionPlannerRoutes.Configuration);
    }

    [RelayCommand(CanExecute = nameof(CanReboot))]
    private async Task RebootAsync()
    {
        var id = activeVehicle.VehicleId;
        var token = activeVehicle.ConnectionCancellationToken;
        if (id is null || !CanReboot || !await confirmation.ConfirmAsync("Reboot vehicle", "Keep the vehicle disarmed and stationary. Reboot now?", "Reboot"))
        {
            return;
        }
        try
        {
            token.ThrowIfCancellationRequested();
            if (activeVehicle.VehicleId != id || !CanReboot)
            {
                return;
            }
            var response = await commands.RebootAutopilotAsync(id.Value, true, token);
            rebootRequested = response.Result == VehicleCommandResult.Accepted;
            SetMessages(response.Message ?? $"Reboot: {response.Result}");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartCalibrationAsync()
    {
        var id = activeVehicle.VehicleId;
        var token = activeVehicle.ConnectionCancellationToken;
        if (id is null || !CanStart || !await confirmation.ConfirmAsync("Start compass calibration", "Move away from metal and magnetic interference. Rotate the vehicle through all orientations. Continue?", "Start calibration"))
        {
            return;
        }
        try
        {
            token.ThrowIfCancellationRequested();
            if (activeVehicle.VehicleId == id && CanStart)
            {
                await calibration.StartAsync(id.Value, false, token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
    }

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private async Task AcceptCalibrationAsync()
    {
        try
        {
            await calibration.AcceptAsync(activeVehicle.ConnectionCancellationToken);
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private async Task CancelCalibrationAsync()
    {
        try
        {
            await calibration.CancelAsync();
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
    }

    [RelayCommand]
    private void Reset()
    {
        calibration.Reset();
    }

    private void ShowCalibration(CompassCalibrationSnapshot snapshot)
    {
        CalibrationState = snapshot.State;
        Instruction = snapshot.Instruction;
        Progress = snapshot.OverallProgress;
        QualitySummary = snapshot.QualitySummary;
        ProgressSummary = string.Join(Environment.NewLine, snapshot.Progress.Select(p => $"Compass {p.CompassId + 1}: {p.Status} ({p.CompletionPercent}%)"));
        if (snapshot.FailureReason is not null)
        {
            SetMessages(snapshot.FailureReason);
        }
        if (snapshot.State == CompassCalibrationWorkflowState.Success && snapshot.VehicleId is { } id && activeVehicle.State is { } state && state.VehicleId == id)
        {
            completionStore.Save(workflowCatalog.CreateEvidence(SetupWorkflowKey.Compass, state, parameterRegistry.GetAllParameters(id), clock.UtcNow));
        }
        NotifyAvailability();
    }

    private void NotifyAvailability()
    {
        UpdateDocument();
        foreach (var name in new[] { nameof(CanEdit), nameof(CanStart), nameof(CanAccept), nameof(CanCancel), nameof(CanApply), nameof(CanReboot), nameof(CalibrationAvailability) })
        {
            OnPropertyChanged(name);
        }
        StartCalibrationCommand.NotifyCanExecuteChanged();
        AcceptCalibrationCommand.NotifyCanExecuteChanged();
        CancelCalibrationCommand.NotifyCanExecuteChanged();
        ApplyCommand.NotifyCanExecuteChanged();
        RebootCommand.NotifyCanExecuteChanged();
    }

    private void OnCalibrationChanged(CompassCalibrationStateChangedEventArgs args)
    {
        Dispatcher.Dispatch(() =>
    {
        if (active)
        {
            ShowCalibration(args.Snapshot);
        }
    });
    }

    private void OnVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        Dispatcher.Dispatch(() =>
        {
            if (!active)
            {
                return;
            }
            editVersion++;
            loadVersion++;
            if (rebootRequested && !args.Previous.IsOnline && args.Current.IsOnline)
            {
                RequiresReboot = false;
                rebootRequested = false;
            }
            operationCancellation?.Cancel();
            review = null;
            if (args.Previous.VehicleId != args.Current.VehicleId || (args.Previous.State is not null && args.Current.State is not null && args.Previous.State.Identity.Firmware != args.Current.State.Identity.Firmware))
            {
                current = null;
                desired = null;
                HasPendingChanges = false;
                HasConflict = false;
                RequiresReboot = false;
                Settings.Clear();
                PendingChanges.Clear();
            }
            NotifyAvailability();
            _ = ReloadAndReviewAsync();
        });
    }

    private async Task ReloadAndReviewAsync()
    {
        await LoadAsync();
        if (HasPendingChanges && !HasConflict)
        {
            await EvaluateAsync(++editVersion);
        }
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        if (active)
        {
            return;
        }
        active = true;
        calibration.StateChanged += OnCalibrationChanged;
        activeVehicle.Changed += OnVehicleChanged;
        loadSubscription = domain.SubscribeDomainEventAsync<VehicleParameterLoadStatusChanged>((change, cancellationToken) =>
        {
            if (active && change.Status.VehicleId == activeVehicle.VehicleId)
            {
                Dispatcher.Dispatch(() =>
                {
                    if (active)
                    {
                        _ = ReloadAndReviewAsync();
                    }
                });
            }
            return Task.CompletedTask;
        });
        ShowCalibration(calibration.Current);
        await base.ActivateAsync();
        await LoadAsync();
        refreshTimer = new Timer(timerState => Dispatcher.Dispatch(() =>
        {
            if (active)
            {
                _ = LoadAsync();
            }
        }), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        active = false;
        loadVersion++;
        editVersion++;
        refreshTimer?.Dispose();
        refreshTimer = null;
        loadSubscription?.Dispose();
        loadSubscription = null;
        activeVehicle.Changed -= OnVehicleChanged;
        calibration.StateChanged -= OnCalibrationChanged;
        operationCancellation?.Cancel();
        await calibration.CancelAsync();
        Discard();
        await base.DeactivateAsync();
    }

    /// <summary>Cancels local apply work.</summary>
    public void Cancel()
    {
        operationCancellation?.Cancel();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        active = false;
        loadVersion++;
        editVersion++;
        refreshTimer?.Dispose();
        loadSubscription?.Dispose();
        activeVehicle.Changed -= OnVehicleChanged;
        calibration.StateChanged -= OnCalibrationChanged;
        operationCancellation?.Cancel();
        calibration.Dispose();
        base.Dispose();
    }
}
