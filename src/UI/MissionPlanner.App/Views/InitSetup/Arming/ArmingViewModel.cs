using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Presentation.Documents;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Setup.Arming;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Arming;

/// <summary>Arming setup presentation with connection-scoped pending edits and existing guarded commands.</summary>
public sealed partial class ArmingViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext active;
    private readonly IArmingConfigurationService configuration;
    private readonly IVehicleLiveDiagnostics diagnostics;
    private readonly IVehicleCommandService commands;
    private readonly IVehicleCommandPolicy policy;
    private readonly IUserConfirmationService confirmation;
    private readonly IArmingSetupDocumentFactory documents;
    private readonly INavigationService navigation;
    private readonly IReplaySessionManager replay;
    private readonly TimeProvider clock;
    private readonly RadioSwitchMovement movement = new();
    private CancellationTokenSource? lifetime;
    private CancellationTokenSource? operation;
    private ArmingSetupState? current;
    private ArmingConfiguration? desired;
    private ArmingChangeSet? review;
    private object? firmwareIdentity;
    private string? documentKey;
    private int generation;
    private int editVersion;
    private bool refreshing;
    private ArmingChoice? selectedSwitch;
    private bool projecting;
    private bool replayBlocked;
    private Guid replaySession;

    /// <summary>Uses the application's existing diagnostic, command, configuration and navigation services.</summary>
    public ArmingViewModel(IActiveVehicleContext active, IArmingConfigurationService configuration,
        IVehicleLiveDiagnostics diagnostics, IVehicleCommandService commands, IVehicleCommandPolicy policy,
        IUserConfirmationService confirmation, IArmingSetupDocumentFactory documents, INavigationService navigation,
        IReplaySessionManager replay, TimeProvider clock, ILogger<ArmingViewModel> logger,
        IUiDispatcher dispatcher, IDomainEventHub events) : base(logger, dispatcher, events)
    {
        this.active = active;
        this.configuration = configuration;
        this.diagnostics = diagnostics;
        this.commands = commands;
        this.policy = policy;
        this.confirmation = confirmation;
        this.documents = documents;
        this.navigation = navigation;
        this.replay = replay;
        this.clock = clock;
        UpdateDocuments();
    }

    /// <summary>Feature-specific semantic setting rows.</summary>
    public ObservableCollection<ArmingSettingViewModel> Settings { get; } = [];
    /// <summary>Explicit reviewed parameter changes.</summary>
    public ObservableCollection<string> PendingChanges { get; } = [];
    /// <summary>None plus supported receiver channel numbers; conflicts remain visible during review.</summary>
    public IReadOnlyList<ArmingChoice> SwitchChoices { get; } = Enumerable.Range(0, 17).Select(c => new ArmingChoice(c, c == 0 ? "None" : $"RC{c}")).ToArray();
    /// <summary>Locally staged receiver switch, never an immediate write.</summary>
    public ArmingChoice? SelectedSwitch
    {
        get => selectedSwitch;
        set
        {
            if (SetProperty(ref selectedSwitch, value) && !projecting && value is not null && desired is not null)
            {
                Stage(desired with { ArmSwitch = (int)value.Value });
            }
        }
    }
    /// <summary>Persistent live summary.</summary>
    [ObservableProperty] public partial string Summary { get; private set; } = "Disconnected";
    /// <summary>Current parameter availability.</summary>
    [ObservableProperty] public partial string ConfigurationStatus { get; private set; } = "Connect a vehicle.";
    /// <summary>Inline review conflicts and warnings.</summary>
    [ObservableProperty] public partial string Validation { get; private set; } = string.Empty;
    /// <summary>Live receiver evidence, not proof of arming.</summary>
    [ObservableProperty] public partial string Movement { get; private set; } = "No switch movement observed.";
    /// <summary>Local operation history, separate from current blockers.</summary>
    [ObservableProperty] public partial string OperationMessage { get; private set; } = string.Empty;
    /// <summary>Status document.</summary>
    [ObservableProperty] public partial UserDocument? StatusDocument { get; private set; }
    /// <summary>Advanced evidence document.</summary>
    [ObservableProperty] public partial UserDocument? DiagnosticDocument { get; private set; }
    /// <summary>Local unsaved configuration.</summary>
    [ObservableProperty] public partial bool HasPendingChanges { get; private set; }
    /// <summary>Current state differs from the reviewed baseline.</summary>
    [ObservableProperty] public partial bool HasConflict { get; private set; }
    /// <summary>Confirmed writes require reboot.</summary>
    [ObservableProperty] public partial bool RequiresReboot { get; private set; }
    /// <summary>Editing requires complete parameters and an online disarmed live vehicle.</summary>
    public bool CanEdit => lifetime is not null && !IsBusy && active.IsOnline && active.State?.IsArmed == false &&
        !replay.Snapshot.IsTransmissionProhibited && current is { ParametersReady: true, IsSupported: true };
    /// <summary>Simple reassignment is unavailable when current assignments are ambiguous.</summary>
    public bool CanAssignSwitch => CanEdit && current is { AssignmentsKnown: true } && current.ArmSwitches.Count <= 1;
    /// <summary>Only a current, conflict-free review can be applied.</summary>
    public bool CanApply => CanEdit && !HasConflict && review?.CanApply == true;
    /// <summary>Availability is derived from the existing command policy.</summary>
    public bool CanArm => CanAct(VehicleAction.Arm);
    /// <summary>Availability is derived from the existing command policy.</summary>
    public bool CanDisarm => CanAct(VehicleAction.Disarm);
    /// <summary>Why GCS arming is unavailable, without inventing readiness.</summary>
    public string ArmAvailability => !active.IsOnline ? "GCS Arm: vehicle offline." : replay.Snapshot.IsTransmissionProhibited ? "Replay is read-only." :
        active.State is { } state ? policy.Evaluate(state, VehicleAction.Arm).Reason ?? "GCS Arm is available through the normal command policy." : "Vehicle state unavailable.";
    private bool CanAct(VehicleAction action) => lifetime is not null && !IsBusy && active.IsOnline && !replay.Snapshot.IsTransmissionProhibited &&
        active.State is { } state && policy.Evaluate(state, action).IsAllowed;

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        if (lifetime is not null)
        {
            return;
        }
        var activation = new CancellationTokenSource();
        lifetime = activation;
        active.Changed += BoundaryChanged;
        replay.Changed += ReplayChanged;
        replayBlocked = replay.Snapshot.IsTransmissionProhibited;
        replaySession = replay.Snapshot.SessionId;
        await RefreshAsync();
        if (ReferenceEquals(lifetime, activation))
        {
            _ = PollAsync(activation.Token);
        }
    }
    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        active.Changed -= BoundaryChanged;
        replay.Changed -= ReplayChanged;
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = null;
        ResetBoundary();
        return Task.CompletedTask;
    }
    private async Task PollAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(token))
            {
                await Dispatcher.DispatchAsync(RefreshAsync);
            }
        }
        catch (OperationCanceledException) { }
    }
    private void BoundaryChanged(ActiveVehicleChangedEventArgs args) => Dispatcher.Dispatch(() =>
    {
        if (lifetime is not null)
        {
            ResetBoundary();
            _ = RefreshAsync();
        }
    });
    private void ReplayChanged(ReplaySessionChangedEventArgs args) => Dispatcher.Dispatch(() =>
    {
        if (lifetime is not null && (replayBlocked != args.Snapshot.IsTransmissionProhibited || replaySession != args.Snapshot.SessionId))
        {
            replayBlocked = args.Snapshot.IsTransmissionProhibited;
            replaySession = args.Snapshot.SessionId;
            ResetBoundary();
            _ = RefreshAsync();
        }
    });
    private void ResetBoundary()
    {
        generation++;
        editVersion++;
        operation?.Cancel();
        current = null;
        firmwareIdentity = null;
        desired = null;
        review = null;
        movement.Clear();
        Settings.Clear();
        PendingChanges.Clear();
        projecting = true;
        SelectedSwitch = null;
        projecting = false;
        HasPendingChanges = false;
        HasConflict = false;
        RequiresReboot = false;
        IsBusy = false;
        OperationMessage = string.Empty;
        Validation = string.Empty;
        ConfigurationStatus = "Waiting for current vehicle configuration.";
        UpdateDocuments();
        NotifyAvailability();
    }
    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (lifetime is null || refreshing)
        {
            return;
        }
        if (IsBusy)
        {
            UpdateDocuments();
            return;
        }
        if (!active.IsOnline || active.VehicleId is not { } id)
        {
            UpdateDocuments();
            NotifyAvailability();
            return;
        }
        refreshing = true;
        var version = generation;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, active.ConnectionCancellationToken);
        try
        {
            var state = await configuration.ReadAsync(id, linked.Token);
            if (linked.IsCancellationRequested || version != generation || lifetime is null || active.VehicleId != id)
            {
                return;
            }
            var identity = active.State?.Identity.Firmware;
            if (firmwareIdentity is not null && !Equals(firmwareIdentity, identity))
            {
                ResetBoundary();
            }
            firmwareIdentity = identity;
            var changed = current?.Current != state.Current || current is null ||
                !System.Text.Json.JsonSerializer.Serialize(current.Settings).Equals(System.Text.Json.JsonSerializer.Serialize(state.Settings), StringComparison.Ordinal);
            if (HasPendingChanges && current?.Current != state.Current)
            {
                HasConflict = true;
                Validation = "Confirmed values changed. Discard and review again.";
            }
            current = state;
            ConfigurationStatus = state.Status;
            if (!HasPendingChanges)
            {
                desired = state.Current;
            }
            if (changed)
            {
                ProjectEditors();
            }
            UpdateDocuments();
            NotifyAvailability();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (version == generation)
            {
                ConfigurationStatus = ex.Message;
            }
        }
        finally
        {
            refreshing = false;
        }
    }
    private void ProjectEditors()
    {
        if (current is null || desired is null)
        {
            return;
        }
        Settings.Clear();
        foreach (var definition in current.Settings)
        {
            Settings.Add(new(definition, desired, next => Stage(desired.WithEditorValue(definition.Setting, next.EditorValue(definition.Setting) ?? 0) with
            { CustomChecks = definition.Setting == ArmingSetting.Checks ? next.CustomChecks : desired.CustomChecks })));
        }
        projecting = true;
        SelectedSwitch = SwitchChoices.FirstOrDefault(c => c.Value == desired.ArmSwitch);
        projecting = false;
    }
    private void Stage(ArmingConfiguration next)
    {
        if (!CanEdit)
        {
            return;
        }
        desired = next;
        HasPendingChanges = desired != current?.Current;
        review = null;
        ProjectEditors();
        _ = ReviewAsync(++editVersion);
        NotifyAvailability();
        UpdateDocuments();
    }
    private async Task ReviewAsync(int edit)
    {
        if (desired is null || active.VehicleId is not { } id || lifetime is null)
        {
            return;
        }
        var version = generation;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, active.ConnectionCancellationToken);
        try
        {
            var result = await configuration.EvaluateChangesAsync(id, desired, linked.Token);
            if (linked.IsCancellationRequested || version != generation || edit != editVersion)
            {
                return;
            }
            review = result;
            PendingChanges.Clear();
            foreach (var change in result.Changes)
            {
                PendingChanges.Add($"{change.Name}: {change.OldValue} → {change.NewValue}" + (change.RequiresReboot ? " · Reboot required" : string.Empty));
            }
            Validation = string.Join(Environment.NewLine, result.Errors.Concat(result.Warnings));
            NotifyAvailability();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (version == generation && edit == editVersion)
            {
                Validation = ex.Message;
            }
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
        Validation = string.Empty;
        ProjectEditors();
        UpdateDocuments();
        NotifyAvailability();
    }
    private void NotifyAvailability()
    {
        OnPropertyChanged(nameof(CanEdit));
        OnPropertyChanged(nameof(CanAssignSwitch));
        OnPropertyChanged(nameof(CanApply));
        OnPropertyChanged(nameof(CanArm));
        OnPropertyChanged(nameof(CanDisarm));
        OnPropertyChanged(nameof(ArmAvailability));
        ApplyCommand.NotifyCanExecuteChanged();
        ArmCommand.NotifyCanExecuteChanged();
        DisarmCommand.NotifyCanExecuteChanged();
    }
    private void UpdateDocuments()
    {
        var online = lifetime is not null && active.IsOnline;
        var diagnostic = online && active.VehicleId is { } id ? diagnostics.GetArming(id) : null;
        var freshRc = online && active.State?.Radio.IsStale(clock.GetUtcNow(), TimeSpan.FromSeconds(3)) == false;
        if (freshRc)
        {
            movement.Observe(active.State!.Radio);
        }
        var channel = desired?.ArmSwitch ?? current?.Current.ArmSwitch ?? 0;
        Movement = (freshRc ? "RC input live. " : "RC input stale or unavailable. ") +
            (channel > 0 ? movement.Describe(channel) ?? $"Move the selected RC{channel} switch through low/high/low." : "Choose a switch channel to observe movement.");
        Summary = !online ? "Disconnected" : (diagnostic?.Summary ?? "Arming state unknown") + " · " + (freshRc ? "RC input live" : "RC input unavailable") +
            (current?.ArmSwitches.Count > 0 ? " · Arm switch " + string.Join(", ", current.ArmSwitches.Select(c => $"RC{c}")) : string.Empty);
        var context = new ArmingDocumentContext(online, current, diagnostic, HasPendingChanges, Movement, OperationMessage, ArmAvailability);
        // Ignore unrelated high-rate telemetry and preserve selection while report evidence is unchanged.
        var key = System.Text.Json.JsonSerializer.Serialize(context);
        if (key == documentKey)
        {
            return;
        }
        documentKey = key;
        var status = documents.CreateStatus(context);
        var detail = documents.CreateDiagnostics(context);
        if (StatusDocument?.Markdown != status.Markdown) { StatusDocument = status; }
        if (DiagnosticDocument?.Markdown != detail.Markdown) { DiagnosticDocument = detail; }
    }
    /// <inheritdoc />
    public override void Dispose()
    {
        _ = DeactivateAsync();
        base.Dispose();
    }
}
