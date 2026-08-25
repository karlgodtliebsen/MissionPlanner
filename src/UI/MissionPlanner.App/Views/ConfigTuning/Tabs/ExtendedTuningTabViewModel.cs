using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.ConfigTuning.Tuning;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Shared.Models.Vehicles.Models;
using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Coordinates the active-vehicle Extended Tuning workspace.</summary>
public sealed partial class ExtendedTuningTabViewModel : BaseViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IExtendedTuningService tuningService;
    private readonly IControlResponseMetricsService metricsService;
    private readonly IUserConfirmationService confirmation;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<ExtendedTuningTabViewModel> logger;
    private ExtendedTuningWorkspace? workspace;
    private CancellationTokenSource? operationCancellation;
    private ActiveProfileKey activeKey;
    private bool active;
    private bool disposed;

    /// <summary>Initializes the Extended Tuning page.</summary>
    /// <param name="activeVehicle">The active-vehicle context.</param>
    /// <param name="tuningService">The advanced tuning service.</param>
    /// <param name="metricsService">The read-only control-response service.</param>
    /// <param name="confirmation">The expert-change confirmation service.</param>
    /// <param name="dispatcher">The UI Dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public ExtendedTuningTabViewModel(
        IActiveVehicleContext activeVehicle,
        IExtendedTuningService tuningService,
        IControlResponseMetricsService metricsService,
        IUserConfirmationService confirmation,
        IDispatcher dispatcher,
        ILogger<ExtendedTuningTabViewModel> logger) : base(logger)
    {
        this.activeVehicle = activeVehicle;
        this.tuningService = tuningService;
        this.metricsService = metricsService;
        this.confirmation = confirmation;
        this.dispatcher = dispatcher;
        this.logger = logger;
        StatusMessage = "Connect a vehicle to use Extended Tuning.";
    }

    /// <summary>Gets all lazy descriptor groups.</summary>
    public ObservableRangeCollection<ExtendedTuningGroupViewModel> Groups
    {
        get;
    } = [];

    /// <summary>Gets descriptor groups matching the current curated-set search.</summary>
    public ObservableRangeCollection<ExtendedTuningGroupViewModel> VisibleGroups
    {
        get;
    } = [];

    /// <summary>Gets read-only response telemetry for the active vehicle.</summary>
    public ObservableRangeCollection<ControlResponseMetricViewModel> ResponseMetrics
    {
        get;
    } = [];

    /// <summary>Gets or sets the advanced curated-set search.</summary>
    [ObservableProperty]
    public partial string SearchText
    {
        get;
        set;
    } = string.Empty;

    /// <summary>Gets whether an operation is running.</summary>
    [ObservableProperty]
    public new partial bool IsBusy
    {
        get;
        private set;
    }

    /// <summary>Gets whether the vehicle is connected.</summary>
    [ObservableProperty]
    public partial bool IsConnected
    {
        get;
        private set;
    }

    /// <summary>Gets whether a supported advanced profile is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnsupported))]
    public partial bool HasSupportedProfile
    {
        get;
        private set;
    }

    /// <summary>Gets whether the connected firmware has no supported advanced profile.</summary>
    public bool IsUnsupported => IsConnected && !HasSupportedProfile;

    /// <summary>Gets the connected firmware family.</summary>
    [ObservableProperty]
    public partial string FirmwareFamilyText
    {
        get;
        private set;
    } = "No vehicle connected";

    /// <summary>Gets the number of pending advanced parameter edits.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial int ModifiedCount
    {
        get;
        private set;
    }

    /// <summary>Gets whether advanced fields contain pending edits.</summary>
    public bool HasPendingChanges => ModifiedCount > 0;

    /// <summary>Gets a reviewable summary of pending advanced edits.</summary>
    [ObservableProperty]
    public partial string ChangeSummary
    {
        get;
        private set;
    } = "No pending advanced changes.";



    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        DeactivateAsync();
        DetachWorkspace();
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (active)
        {
            return Task.CompletedTask;
        }

        active = true;
        activeVehicle.Changed += OnActiveVehicleChanged;
        metricsService.Changed += OnMetricChanged;
        dispatcher.Dispatch(() => _ = InitializeAsync());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        if (!active)
        {
            return Task.CompletedTask;
        }

        active = false;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        metricsService.Changed -= OnMetricChanged;
        CancelOperation();
        return Task.CompletedTask;
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterGroups();
    }

    private async Task ApplyGroupAsync(ExtendedTuningGroupViewModel group)
    {
        if (workspace is null || IsBusy)
        {
            return;
        }

        await RunAsync(async cancellationToken =>
        {
            var groupChanges = group.ParameterNames
                .Select(name => workspace.Session.GetField(name))
                .Where(item => item?.IsModified == true)
                .Select(item => $"{item!.Name}: {item.LiveValue:G} → {item.PendingValue:G}")
                .ToArray();
            if (groupChanges.Length == 0)
            {
                StatusMessage = $"{group.Title} has no pending changes.";
                return;
            }

            if (!await confirmation.ConfirmAsync(
                    "Apply expert tuning changes?",
                    $"{group.ExpertWarning}\n\n{string.Join(Environment.NewLine, groupChanges)}",
                    "Apply expert changes",
                    cancellationToken))
            {
                StatusMessage = "Advanced tuning changes were not applied.";
                return;
            }

            var result = await tuningService.ApplyGroupAsync(workspace, group.Key, cancellationToken);
            group.ValidationMessage = result.ValidationIssues.Count == 0
                ? null
                : string.Join(" ", result.ValidationIssues.Select(issue => issue.Message));
            RefreshState();
            StatusMessage = result.Success
                ? $"{group.Title} applied and confirmed. Flight-test cautiously."
                : group.ValidationMessage ?? $"{group.Title} was not fully confirmed; failed fields remain pending.";
        }).ConfigureAwait(false);
    }

    private void RevertGroup(ExtendedTuningGroupViewModel group)
    {
        if (workspace is null || IsBusy)
        {
            return;
        }

        tuningService.RevertGroup(workspace, group.Key);
        group.ValidationMessage = null;
        group.ClearCopyPreview();
        RefreshState();
        StatusMessage = $"Pending changes in {group.Title} were reverted.";
    }

    private async Task RefreshGroupAsync(ExtendedTuningGroupViewModel group)
    {
        if (workspace is null || IsBusy)
        {
            return;
        }

        await RunAsync(async cancellationToken =>
        {
            await workspace.Session.RefreshAsync(group.ParameterNames, cancellationToken);
            RefreshState();
            StatusMessage = $"Refresh requested for {group.Title}.";
        }).ConfigureAwait(false);
    }

    private void PreviewCopy(ExtendedTuningGroupViewModel group)
    {
        if (workspace is null || group.SelectedSourceAxis is null || group.SelectedTargetAxis is null)
        {
            return;
        }

        try
        {
            group.SetCopyPreview(tuningService.PreviewCopyAxis(
                workspace,
                group.Key,
                group.SelectedSourceAxis,
                group.SelectedTargetAxis));
            StatusMessage = "Axis copy preview created. Review every target value; no pending value has changed yet.";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private async Task ApplyCopyAsync(ExtendedTuningGroupViewModel group)
    {
        if (workspace is null || group.PendingCopyPreview is not { } preview || IsBusy)
        {
            return;
        }

        var summary = string.Join(Environment.NewLine, preview.Changes.Select(change =>
            $"{change.TargetParameter}: {change.TargetValue:G} → {change.SourceValue:G}"));
        if (!await confirmation.ConfirmAsync(
                "Apply axis copy preview?",
                $"This updates pending values only; use Apply group to write them to the vehicle.\n\n{summary}",
                "Apply to pending",
                activeVehicle.ConnectionCancellationToken))
        {
            return;
        }

        var result = tuningService.ApplyCopyAxisPreview(workspace, preview);
        if (result.Success)
        {
            group.ClearCopyPreview();
            RefreshState();
            StatusMessage = "Reviewed axis values copied to pending state. Review the change summary before applying the group.";
        }
        else
        {
            StatusMessage = string.Join(" ", result.Errors);
        }
    }

    private async Task InitializeAsync()
    {
        CancelOperation();
        DetachWorkspace();
        Groups.Clear();
        VisibleGroups.Clear();
        ResponseMetrics.Clear();
        var snapshot = activeVehicle.Current;
        activeKey = ActiveProfileKey.From(snapshot);
        IsConnected = snapshot.IsOnline;
        FirmwareFamilyText = snapshot.State?.Identity.Firmware.Family.ToString() ?? "No vehicle connected";
        HasSupportedProfile = false;
        if (!snapshot.IsOnline || snapshot.VehicleId is not { } vehicleId)
        {
            StatusMessage = "Connect a vehicle to use Extended Tuning.";
            return;
        }

        await RunAsync(async cancellationToken =>
        {
            workspace = await tuningService.OpenAsync(vehicleId, cancellationToken);
            if (workspace is null || workspace.Groups.Count == 0)
            {
                StatusMessage = $"No curated advanced fields are present for {FirmwareFamilyText}.";
                return;
            }

            workspace.Session.Changed += OnSessionChanged;
            var groups = new List<ExtendedTuningGroupViewModel>();
            foreach (var item in workspace.Groups)
            {
                groups.Add(new ExtendedTuningGroupViewModel(
                    item,
                    workspace.Session,
                    tuningService,
                    ApplyGroupAsync,
                    RevertGroup,
                    RefreshGroupAsync,
                    PreviewCopy,
                    ApplyCopyAsync));
            }

            Groups.ReplaceRange(groups);

            HasSupportedProfile = true;
            FilterGroups();
            RefreshMetrics(vehicleId);
            RefreshState();
            StatusMessage = $"Loaded {Groups.Count} lazy advanced groups for {FirmwareFamilyText}. Expand only the controller you intend to review.";
        }).ConfigureAwait(false);
    }

    private new async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        CancelOperation();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        operationCancellation = cancellation;
        IsBusy = true;
        try
        {
            await operation(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            StatusMessage = activeVehicle.IsOnline ? "Extended Tuning operation cancelled." : "Vehicle disconnected; Extended Tuning operation cancelled.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Extended Tuning operation failed.");
            StatusMessage = exception.Message;
        }
        finally
        {
            if (ReferenceEquals(operationCancellation, cancellation))
            {
                operationCancellation = null;
                IsBusy = false;
            }
        }
    }

    private void FilterGroups()
    {
        var search = SearchText.Trim();
        var visibleGroups = new List<ExtendedTuningGroupViewModel>();

        foreach (var group in Groups.Where(group => group.Matches(search)))
        {
            group.SetFilter(search, !string.IsNullOrWhiteSpace(search));
            visibleGroups.Add(group);
        }

        VisibleGroups.ReplaceRange(visibleGroups);
    }

    private void RefreshState()
    {
        if (workspace is null)
        {
            ModifiedCount = 0;
            ChangeSummary = "No pending advanced changes.";
            return;
        }

        foreach (var group in Groups)
        {
            group.Refresh();
            var issues = tuningService.ValidateGroup(workspace, group.Key);
            group.ValidationMessage = issues.Count == 0 ? null : string.Join(" ", issues.Select(issue => issue.Message));
        }

        var modified = workspace.Groups
            .SelectMany(group => group.Fields)
            .Select(item => workspace.Session.GetField(item.ParameterName))
            .Where(item => item?.IsModified == true)
            .DistinctBy(item => item!.Name, StringComparer.Ordinal)
            .ToArray();
        ModifiedCount = modified.Length;
        ChangeSummary = modified.Length == 0
            ? "No pending advanced changes."
            : string.Join(Environment.NewLine, modified.Select(item =>
                $"{item!.Name}: {item.LiveValue:G} → {item.PendingValue:G}"));
    }

    private void RefreshMetrics(VehicleId vehicleId)
    {
        var responseMetrics = new List<ControlResponseMetricViewModel>();
        foreach (var metric in metricsService.GetMetrics(vehicleId))
        {
            responseMetrics.Add(ToMetricViewModel(metric));
        }

        ResponseMetrics.ReplaceRange(responseMetrics);
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        var next = ActiveProfileKey.From(args.Current);
        if (next != activeKey)
        {
            dispatcher.Dispatch(() => _ = InitializeAsync());
        }
    }

    private void OnSessionChanged()
    {
        dispatcher.Dispatch(RefreshState);
    }

    private void OnMetricChanged(object? sender, ControlResponseMetricChangedEventArgs args)
    {
        if (args.Metric.VehicleId == activeVehicle.VehicleId)
        {
            dispatcher.Dispatch(() => RefreshMetrics(args.Metric.VehicleId));
        }
    }

    private void DetachWorkspace()
    {
        workspace?.Session.Changed -= OnSessionChanged;
        workspace = null;
    }

    private void CancelOperation()
    {
        operationCancellation?.Cancel();
        operationCancellation = null;
        IsBusy = false;
    }

    private static ControlResponseMetricViewModel ToMetricViewModel(ControlResponseMetric metric)
    {
        return new ControlResponseMetricViewModel(
            metric.Axis.ToString(System.Globalization.CultureInfo.InvariantCulture),
            metric.Desired,
            metric.Achieved,
            metric.Error,
            $"FF {metric.FeedForward:G4} · P {metric.Proportional:G4} · I {metric.Integral:G4} · D {metric.Derivative:G4}");
    }

    private readonly record struct ActiveProfileKey(VehicleId? VehicleId, bool IsOnline, VehicleFirmwareIdentity? Firmware)
    {
        public static ActiveProfileKey From(ActiveVehicleSnapshot snapshot)
        {
            return new ActiveProfileKey(snapshot.VehicleId, snapshot.IsOnline, snapshot.State?.Identity.Firmware);
        }
    }
}
