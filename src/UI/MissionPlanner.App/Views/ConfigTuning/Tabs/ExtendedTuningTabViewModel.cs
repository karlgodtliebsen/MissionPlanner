using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Core.Configuration.Tuning;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Projects one metadata-backed advanced tuning field.</summary>
public sealed partial class AdvancedTuningFieldViewModel : ObservableObject
{
    /// <summary>Initializes an advanced field projection.</summary>
    /// <param name="resolved">The resolved field.</param>
    /// <param name="session">The shared parameter session.</param>
    public AdvancedTuningFieldViewModel(ResolvedAdvancedTuningField resolved, IParameterEditSession session)
    {
        Definition = resolved.Definition;
        ParameterName = resolved.ParameterName;
        Editor = new ParameterItemViewModel(session, session.GetField(ParameterName)!);
    }

    /// <summary>Gets the expanded field definition.</summary>
    public AdvancedTuningFieldDefinition Definition { get; }

    /// <summary>Gets the resolved parameter name.</summary>
    public string ParameterName { get; }

    /// <summary>Gets the shared-session editor.</summary>
    public ParameterItemViewModel Editor { get; }

    /// <summary>Gets the axis label, when applicable.</summary>
    public string AxisText => string.IsNullOrWhiteSpace(Definition.Axis) ? string.Empty : $"Axis {Definition.Axis}";

    /// <summary>Gets the instance label, when applicable.</summary>
    public string InstanceText => Definition.Instance == 0 ? string.Empty : $"Instance {Definition.Instance}";

    /// <summary>Gets the component title.</summary>
    public string Title => Definition.Component.Title;

    /// <summary>Gets the component explanation.</summary>
    public string Description => Definition.Component.Description;

    /// <summary>Gets metadata units with a descriptor fallback.</summary>
    public string Units => string.IsNullOrWhiteSpace(Editor.Units)
        ? Definition.Component.FallbackUnits
        : Editor.Units;

    /// <summary>Gets the normalized pending magnitude for axis comparisons.</summary>
    [ObservableProperty]
    public partial double NormalizedMagnitude { get; set; }

    /// <summary>Refreshes the editor from shared state.</summary>
    /// <param name="session">The shared parameter session.</param>
    public void Refresh(IParameterEditSession session)
    {
        if (session.GetField(ParameterName) is { } state)
        {
            Editor.SetField(state);
        }
    }
}

/// <summary>Projects one proposed axis-copy change for explicit review.</summary>
/// <param name="Component">The copied component.</param>
/// <param name="SourceParameter">The source parameter.</param>
/// <param name="TargetParameter">The target parameter.</param>
/// <param name="BeforeValue">The current target pending value.</param>
/// <param name="AfterValue">The proposed source value.</param>
public sealed record AxisCopyChangeViewModel(
    string Component,
    string SourceParameter,
    string TargetParameter,
    double BeforeValue,
    double AfterValue);

/// <summary>Projects one read-only live control-response metric.</summary>
/// <param name="Axis">The protocol axis label.</param>
/// <param name="Desired">The desired response.</param>
/// <param name="Achieved">The achieved response.</param>
/// <param name="Error">The response error.</param>
/// <param name="Contributions">The controller contribution summary.</param>
public sealed record ControlResponseMetricViewModel(
    string Axis,
    float Desired,
    float Achieved,
    float Error,
    string Contributions);

/// <summary>Provides one lazy, searchable advanced descriptor group.</summary>
public sealed partial class ExtendedTuningGroupViewModel : ObservableObject
{
    private readonly ResolvedAdvancedTuningGroup resolved;
    private readonly IParameterEditSession session;
    private readonly IExtendedTuningService service;
    private readonly List<AdvancedTuningFieldViewModel> materialized = [];
    private string filter = string.Empty;

    /// <summary>Initializes a lazy descriptor group.</summary>
    /// <param name="resolved">The presence-gated descriptor.</param>
    /// <param name="session">The shared parameter session.</param>
    /// <param name="service">The advanced tuning service.</param>
    /// <param name="apply">The group apply callback.</param>
    /// <param name="revert">The group revert callback.</param>
    /// <param name="refresh">The group refresh callback.</param>
    /// <param name="previewCopy">The axis-copy preview callback.</param>
    /// <param name="applyCopy">The reviewed-preview apply callback.</param>
    public ExtendedTuningGroupViewModel(
        ResolvedAdvancedTuningGroup resolved,
        IParameterEditSession session,
        IExtendedTuningService service,
        Func<ExtendedTuningGroupViewModel, Task> apply,
        Action<ExtendedTuningGroupViewModel> revert,
        Func<ExtendedTuningGroupViewModel, Task> refresh,
        Action<ExtendedTuningGroupViewModel> previewCopy,
        Func<ExtendedTuningGroupViewModel, Task> applyCopy)
    {
        this.resolved = resolved;
        this.session = session;
        this.service = service;
        Axes = resolved.Axes;
        SelectedSourceAxis = Axes.FirstOrDefault();
        SelectedTargetAxis = Axes.Skip(1).FirstOrDefault();
        ToggleExpandedCommand = new RelayCommand(ToggleExpanded);
        ApplyCommand = new AsyncRelayCommand(() => apply(this));
        RevertCommand = new RelayCommand(() => revert(this));
        RefreshCommand = new AsyncRelayCommand(() => refresh(this));
        PreviewCopyCommand = new RelayCommand(() => previewCopy(this));
        ApplyCopyCommand = new AsyncRelayCommand(() => applyCopy(this));
        Refresh();
    }

    /// <summary>Gets the descriptor key.</summary>
    public string Key => resolved.Descriptor.Key;

    /// <summary>Gets the category.</summary>
    public string Category => resolved.Descriptor.Category;

    /// <summary>Gets the title.</summary>
    public string Title => resolved.Descriptor.Title;

    /// <summary>Gets the description.</summary>
    public string Description => resolved.Descriptor.Description;

    /// <summary>Gets the required expert warning.</summary>
    public string ExpertWarning => resolved.Descriptor.ExpertWarning;

    /// <summary>Gets the number of supported fields without materializing editor rows.</summary>
    public int SupportedFieldCount => resolved.Fields.Count;

    /// <summary>Gets whether copying between axes is supported.</summary>
    public bool SupportsAxisCopy => resolved.Descriptor.SupportsAxisCopy && Axes.Count > 1;

    /// <summary>Gets the present axes.</summary>
    public IReadOnlyList<string> Axes { get; }

    /// <summary>Gets or sets the source copy axis.</summary>
    [ObservableProperty]
    public partial string? SelectedSourceAxis { get; set; }

    /// <summary>Gets or sets the target copy axis.</summary>
    [ObservableProperty]
    public partial string? SelectedTargetAxis { get; set; }

    /// <summary>Gets whether editor rows have been materialized.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; private set; }

    /// <summary>Gets whether this group contains pending changes.</summary>
    [ObservableProperty]
    public partial bool IsModified { get; private set; }

    /// <summary>Gets the current coupled validation message.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    public partial string? ValidationMessage { get; set; }

    /// <summary>Gets whether group validation currently fails.</summary>
    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

    /// <summary>Gets the currently visible, lazily materialized field rows.</summary>
    public ObservableCollection<AdvancedTuningFieldViewModel> Fields { get; } = [];

    /// <summary>Gets the current explicit axis-copy preview rows.</summary>
    public ObservableCollection<AxisCopyChangeViewModel> CopyPreview { get; } = [];

    /// <summary>Gets whether an axis-copy preview awaits explicit application.</summary>
    [ObservableProperty]
    public partial bool HasCopyPreview { get; private set; }

    /// <summary>Gets the expand/collapse command.</summary>
    public IRelayCommand ToggleExpandedCommand { get; }

    /// <summary>Gets the confirmed group apply command.</summary>
    public IAsyncRelayCommand ApplyCommand { get; }

    /// <summary>Gets the group revert command.</summary>
    public IRelayCommand RevertCommand { get; }

    /// <summary>Gets the group refresh command.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Gets the non-mutating axis-copy preview command.</summary>
    public IRelayCommand PreviewCopyCommand { get; }

    /// <summary>Gets the command that applies a reviewed preview to pending values.</summary>
    public IAsyncRelayCommand ApplyCopyCommand { get; }

    internal AxisCopyPreview? PendingCopyPreview { get; private set; }

    /// <summary>Determines whether the descriptor or one of its fields matches a search.</summary>
    /// <param name="search">The search text.</param>
    /// <returns><see langword="true"/> when the group should be shown.</returns>
    public bool Matches(string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        return Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               resolved.Fields.Any(item =>
                   item.ParameterName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                   item.Definition.Component.Title.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Applies a field filter and optionally expands matching rows.</summary>
    /// <param name="search">The search text.</param>
    /// <param name="expand">Whether a matching group should expand.</param>
    public void SetFilter(string search, bool expand)
    {
        filter = search;
        if (expand)
        {
            EnsureMaterialized();
            IsExpanded = true;
        }

        RefreshVisibleFields();
    }

    /// <summary>Refreshes materialized fields, comparison values, and dirty state.</summary>
    public void Refresh()
    {
        foreach (var item in materialized)
        {
            item.Refresh(session);
        }

        IsModified = resolved.Fields.Any(item => session.GetField(item.ParameterName)?.IsModified == true);
        var comparison = service.CompareAxes(
                new ExtendedTuningWorkspace(
                    new ExtendedTuningProfile(session.Scope.FirmwareIdentity.Family, [resolved.Descriptor]),
                    session,
                    [resolved]),
                Key)
            .ToDictionary(item => item.ParameterName, StringComparer.Ordinal);
        foreach (var item in materialized)
        {
            item.NormalizedMagnitude = comparison.GetValueOrDefault(item.ParameterName)?.NormalizedMagnitude ?? 0;
        }
    }

    internal void SetCopyPreview(AxisCopyPreview preview)
    {
        PendingCopyPreview = preview;
        CopyPreview.Clear();
        foreach (var change in preview.Changes)
        {
            CopyPreview.Add(new AxisCopyChangeViewModel(
                change.Component,
                change.SourceParameter,
                change.TargetParameter,
                change.TargetValue,
                change.SourceValue));
        }

        HasCopyPreview = CopyPreview.Count > 0;
    }

    internal void ClearCopyPreview()
    {
        PendingCopyPreview = null;
        CopyPreview.Clear();
        HasCopyPreview = false;
    }

    internal IReadOnlyList<string> ParameterNames => resolved.Fields.Select(item => item.ParameterName).ToArray();

    private void ToggleExpanded()
    {
        if (!IsExpanded)
        {
            EnsureMaterialized();
        }

        IsExpanded = !IsExpanded;
        RefreshVisibleFields();
    }

    private void EnsureMaterialized()
    {
        if (materialized.Count != 0)
        {
            return;
        }

        materialized.AddRange(resolved.Fields.Select(item => new AdvancedTuningFieldViewModel(item, session)));
        Refresh();
    }

    private void RefreshVisibleFields()
    {
        if (!IsExpanded)
        {
            Fields.Clear();
            return;
        }

        var selected = string.IsNullOrWhiteSpace(filter)
            ? materialized
            : materialized.Where(item =>
                item.ParameterName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                item.AxisText.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        Fields.Clear();
        foreach (var item in selected)
        {
            Fields.Add(item);
        }
    }
}

/// <summary>Coordinates the active-vehicle Extended Tuning workspace.</summary>
public sealed partial class ExtendedTuningTabViewModel : ObservableObject, IDisposable
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
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public ExtendedTuningTabViewModel(
        IActiveVehicleContext activeVehicle,
        IExtendedTuningService tuningService,
        IControlResponseMetricsService metricsService,
        IUserConfirmationService confirmation,
        IDispatcher dispatcher,
        ILogger<ExtendedTuningTabViewModel> logger)
    {
        this.activeVehicle = activeVehicle;
        this.tuningService = tuningService;
        this.metricsService = metricsService;
        this.confirmation = confirmation;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    /// <summary>Gets all lazy descriptor groups.</summary>
    public ObservableCollection<ExtendedTuningGroupViewModel> Groups { get; } = [];

    /// <summary>Gets descriptor groups matching the current curated-set search.</summary>
    public ObservableCollection<ExtendedTuningGroupViewModel> VisibleGroups { get; } = [];

    /// <summary>Gets read-only response telemetry for the active vehicle.</summary>
    public ObservableCollection<ControlResponseMetricViewModel> ResponseMetrics { get; } = [];

    /// <summary>Gets or sets the advanced curated-set search.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Gets whether an operation is running.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>Gets whether the vehicle is connected.</summary>
    [ObservableProperty]
    public partial bool IsConnected { get; private set; }

    /// <summary>Gets whether a supported advanced profile is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnsupported))]
    public partial bool HasSupportedProfile { get; private set; }

    /// <summary>Gets whether the connected firmware has no supported advanced profile.</summary>
    public bool IsUnsupported => IsConnected && !HasSupportedProfile;

    /// <summary>Gets the connected firmware family.</summary>
    [ObservableProperty]
    public partial string FirmwareFamilyText { get; private set; } = "No vehicle connected";

    /// <summary>Gets the latest operation status.</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; private set; } = "Connect a vehicle to use Extended Tuning.";

    /// <summary>Gets the number of pending advanced parameter edits.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingChanges))]
    public partial int ModifiedCount { get; private set; }

    /// <summary>Gets whether advanced fields contain pending edits.</summary>
    public bool HasPendingChanges => ModifiedCount > 0;

    /// <summary>Gets a reviewable summary of pending advanced edits.</summary>
    [ObservableProperty]
    public partial string ChangeSummary { get; private set; } = "No pending advanced changes.";

    /// <summary>Activates lifecycle and metric observation.</summary>
    public void Activate()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (active)
        {
            return;
        }

        active = true;
        activeVehicle.Changed += OnActiveVehicleChanged;
        metricsService.Changed += OnMetricChanged;
        dispatcher.Dispatch(() => _ = InitializeAsync());
    }

    /// <summary>Stops lifecycle and metric observation and cancels work.</summary>
    public void Deactivate()
    {
        if (!active)
        {
            return;
        }

        active = false;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        metricsService.Changed -= OnMetricChanged;
        CancelOperation();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Deactivate();
        DetachWorkspace();
    }

    partial void OnSearchTextChanged(string value) => FilterGroups();

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
            foreach (var item in workspace.Groups)
            {
                Groups.Add(new ExtendedTuningGroupViewModel(
                    item,
                    workspace.Session,
                    tuningService,
                    ApplyGroupAsync,
                    RevertGroup,
                    RefreshGroupAsync,
                    PreviewCopy,
                    ApplyCopyAsync));
            }

            HasSupportedProfile = true;
            FilterGroups();
            RefreshMetrics(vehicleId);
            RefreshState();
            StatusMessage = $"Loaded {Groups.Count} lazy advanced groups for {FirmwareFamilyText}. Expand only the controller you intend to review.";
        }).ConfigureAwait(false);
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation)
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
        VisibleGroups.Clear();
        foreach (var group in Groups.Where(group => group.Matches(search)))
        {
            group.SetFilter(search, !string.IsNullOrWhiteSpace(search));
            VisibleGroups.Add(group);
        }
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
        ResponseMetrics.Clear();
        foreach (var metric in metricsService.GetMetrics(vehicleId))
        {
            ResponseMetrics.Add(ToMetricViewModel(metric));
        }
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        var next = ActiveProfileKey.From(args.Current);
        if (next != activeKey)
        {
            dispatcher.Dispatch(() => _ = InitializeAsync());
        }
    }

    private void OnSessionChanged(object? sender, EventArgs args) => dispatcher.Dispatch(RefreshState);

    private void OnMetricChanged(object? sender, ControlResponseMetricChangedEventArgs args)
    {
        if (args.Metric.VehicleId == activeVehicle.VehicleId)
        {
            dispatcher.Dispatch(() => RefreshMetrics(args.Metric.VehicleId));
        }
    }

    private void DetachWorkspace()
    {
        if (workspace is not null)
        {
            workspace.Session.Changed -= OnSessionChanged;
            workspace = null;
        }
    }

    private void CancelOperation()
    {
        operationCancellation?.Cancel();
        operationCancellation = null;
        IsBusy = false;
    }

    private static ControlResponseMetricViewModel ToMetricViewModel(ControlResponseMetric metric) => new(
        metric.Axis.ToString(System.Globalization.CultureInfo.InvariantCulture),
        metric.Desired,
        metric.Achieved,
        metric.Error,
        $"FF {metric.FeedForward:G4} · P {metric.Proportional:G4} · I {metric.Integral:G4} · D {metric.Derivative:G4}");

    private readonly record struct ActiveProfileKey(
        VehicleId? VehicleId,
        bool IsOnline,
        VehicleFirmwareIdentity? Firmware)
    {
        public static ActiveProfileKey From(ActiveVehicleSnapshot snapshot) =>
            new(snapshot.VehicleId, snapshot.IsOnline, snapshot.State?.Identity.Firmware);
    }
}
