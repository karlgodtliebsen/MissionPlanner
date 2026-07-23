using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Fences;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Identifies how clicks on the dedicated fence map modify geometry.</summary>
public enum FenceMapEditMode
{
    /// <summary>Map clicks do not edit fence geometry.</summary>
    None,
    /// <summary>Map clicks append vertices to an inclusion polygon.</summary>
    PolygonInclusion,
    /// <summary>Map clicks append vertices to an exclusion polygon.</summary>
    PolygonExclusion,
    /// <summary>The next map click creates an inclusion circle.</summary>
    CircleInclusion,
    /// <summary>The next map click creates an exclusion circle.</summary>
    CircleExclusion,
    /// <summary>The next map click sets the legacy fence return point.</summary>
    ReturnPoint
}

/// <summary>Projects one fence area into the geometry list.</summary>
/// <param name="Id">The area identifier.</param>
/// <param name="Kind">The area kind.</param>
/// <param name="Summary">A short geometry summary.</param>
/// <param name="IsClosed">Whether polygon editing is complete.</param>
public sealed record FenceAreaListItem(Guid Id, FenceAreaKind Kind, string Summary, bool IsClosed);

/// <summary>Coordinates the shared fence parameter session, local geometry editor, and confirmed transfers.</summary>
public sealed partial class GeoFenceTabViewModel : ObservableObject, IDisposable
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IFenceConfigurationService fenceService;
    private readonly IUserConfirmationService confirmation;
    private readonly ILogger<GeoFenceTabViewModel> logger;
    private CancellationTokenSource? operationCancellation;
    private IParameterEditSession? parameterSession;
    private VehicleId? vehicleId;
    private Guid? draftPolygonId;
    private bool active;
    private bool disposed;

    /// <summary>Initializes the GeoFence configuration workspace.</summary>
    /// <param name="activeVehicle">The active vehicle context.</param>
    /// <param name="fenceService">The fence configuration service.</param>
    /// <param name="confirmation">The hazardous-action confirmation service.</param>
    /// <param name="logger">The logger.</param>
    public GeoFenceTabViewModel(
        IActiveVehicleContext activeVehicle,
        IFenceConfigurationService fenceService,
        IUserConfirmationService confirmation,
        ILogger<GeoFenceTabViewModel> logger)
    {
        this.activeVehicle = activeVehicle;
        this.fenceService = fenceService;
        this.confirmation = confirmation;
        this.logger = logger;
        if (activeVehicle.VehicleId is { } currentVehicle)
        {
            vehicleId = currentVehicle;
            ApplySnapshot(fenceService.GetSnapshot(currentVehicle));
        }
    }

    /// <summary>Gets editable fence parameter rows resolved from live metadata.</summary>
    public ObservableCollection<ParameterItemViewModel> Parameters { get; } = [];

    /// <summary>Gets the local fence areas.</summary>
    public ObservableCollection<FenceAreaListItem> Areas { get; } = [];

    /// <summary>Gets the local fence plan rendered by the dedicated map.</summary>
    [ObservableProperty]
    public partial FencePlan LocalPlan { get; private set; } = FencePlan.Empty;

    /// <summary>Gets whether the target vehicle is connected.</summary>
    [ObservableProperty]
    public partial bool IsConnected { get; private set; }

    /// <summary>Gets whether a parameter or transfer operation is running.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand), nameof(UploadCommand), nameof(ClearVehicleCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Gets whether the vehicle supports typed fence geometry.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTypedGeometryUnsupported))]
    public partial bool SupportsTypedGeometry { get; private set; }

    /// <summary>Gets whether typed fence geometry is unavailable for the connected firmware.</summary>
    public bool IsTypedGeometryUnsupported => !SupportsTypedGeometry;

    /// <summary>Gets whether local geometry differs from the last synchronized vehicle revision.</summary>
    [ObservableProperty]
    public partial bool IsGeometryDirty { get; private set; }

    /// <summary>Gets whether a recoverable pre-replace or pre-clear backup exists.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreBackupCommand))]
    public partial bool HasBackup { get; private set; }

    /// <summary>Gets the current map editing mode.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFenceEditMode), nameof(EditModeText))]
    public partial FenceMapEditMode EditMode { get; private set; }

    /// <summary>Gets whether the map is currently accepting fence edits.</summary>
    public bool IsFenceEditMode => EditMode != FenceMapEditMode.None;

    /// <summary>Gets a description of the current map editing mode.</summary>
    public string EditModeText => EditMode == FenceMapEditMode.None ? "Inspect mode" : $"Editing: {EditMode}";

    /// <summary>Gets or sets the radius used by the next circle map click.</summary>
    [ObservableProperty]
    public partial double CircleRadiusMeters { get; set; } = 100;

    /// <summary>Gets or sets the selected area in the geometry list.</summary>
    [ObservableProperty]
    public partial FenceAreaListItem? SelectedArea { get; set; }

    /// <summary>Gets the local revision label.</summary>
    [ObservableProperty]
    public partial string LocalRevisionText { get; private set; } = "Local revision 0";

    /// <summary>Gets the last synchronized vehicle revision label.</summary>
    [ObservableProperty]
    public partial string VehicleRevisionText { get; private set; } = "Vehicle revision not downloaded";

    /// <summary>Gets the current return-point label.</summary>
    [ObservableProperty]
    public partial string ReturnPointText { get; private set; } = "Return point not set";

    /// <summary>Gets the latest validation or transfer status.</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; private set; } = "Connect a vehicle to configure GeoFence.";

    /// <summary>Gets the current transfer percentage.</summary>
    [ObservableProperty]
    public partial double TransferPercent { get; private set; }

    /// <summary>Occurs when map geometry must be redrawn.</summary>
    public event EventHandler? GeometryChanged;

    /// <summary>Activates connection lifecycle and loads supported fence parameters.</summary>
    public void Activate()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (active)
        {
            return;
        }

        active = true;
        activeVehicle.Changed += OnActiveVehicleChanged;
        _ = InitializeForActiveVehicleAsync();
    }

    /// <summary>Stops connection observation and cancels active work.</summary>
    public void Deactivate()
    {
        if (!active)
        {
            return;
        }

        active = false;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        CancelOperation();
    }

    /// <summary>Applies one map click according to the explicit fence edit mode.</summary>
    /// <param name="latitude">The clicked latitude.</param>
    /// <param name="longitude">The clicked longitude.</param>
    public void HandleMapClick(double latitude, double longitude)
    {
        if (vehicleId is not { } target || EditMode == FenceMapEditMode.None)
        {
            return;
        }

        var position = new GeoPosition(latitude, longitude);
        if (!position.IsValid)
        {
            StatusMessage = "The selected map position is invalid.";
            return;
        }

        var plan = LocalPlan;
        switch (EditMode)
        {
            case FenceMapEditMode.PolygonInclusion:
            case FenceMapEditMode.PolygonExclusion:
                var kind = EditMode == FenceMapEditMode.PolygonInclusion
                    ? FenceAreaKind.PolygonInclusion
                    : FenceAreaKind.PolygonExclusion;
                var areas = plan.Areas.ToList();
                var index = draftPolygonId is { } draft
                    ? areas.FindIndex(area => area.Id == draft)
                    : -1;
                if (index < 0)
                {
                    var area = FenceArea.Polygon(kind, [position]);
                    draftPolygonId = area.Id;
                    areas.Add(area);
                }
                else
                {
                    areas[index] = areas[index] with { Vertices = areas[index].Vertices.Append(position).ToArray() };
                }

                UpdateLocal(target, plan with { Areas = areas });
                StatusMessage = "Vertex added. Add at least three, then finish the polygon.";
                break;
            case FenceMapEditMode.CircleInclusion:
            case FenceMapEditMode.CircleExclusion:
                var circleKind = EditMode == FenceMapEditMode.CircleInclusion
                    ? FenceAreaKind.CircleInclusion
                    : FenceAreaKind.CircleExclusion;
                UpdateLocal(target, plan with { Areas = plan.Areas.Append(FenceArea.Circle(circleKind, position, CircleRadiusMeters)).ToArray() });
                EditMode = FenceMapEditMode.None;
                StatusMessage = "Circle added to the local fence plan.";
                break;
            case FenceMapEditMode.ReturnPoint:
                UpdateLocal(target, plan with { ReturnPoint = position });
                EditMode = FenceMapEditMode.None;
                StatusMessage = "Fence return point updated locally.";
                break;
        }
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
        DetachParameterSession();
    }

    [RelayCommand]
    private void BeginPolygonInclusion() => BeginPolygon(FenceMapEditMode.PolygonInclusion);

    [RelayCommand]
    private void BeginPolygonExclusion() => BeginPolygon(FenceMapEditMode.PolygonExclusion);

    [RelayCommand]
    private void BeginCircleInclusion() => EditMode = FenceMapEditMode.CircleInclusion;

    [RelayCommand]
    private void BeginCircleExclusion() => EditMode = FenceMapEditMode.CircleExclusion;

    [RelayCommand]
    private void BeginReturnPoint() => EditMode = FenceMapEditMode.ReturnPoint;

    [RelayCommand]
    private void FinishPolygon()
    {
        if (vehicleId is not { } target || draftPolygonId is not { } draft)
        {
            return;
        }

        var areas = LocalPlan.Areas.ToList();
        var index = areas.FindIndex(area => area.Id == draft);
        if (index >= 0)
        {
            areas[index] = areas[index] with { IsClosed = true };
            UpdateLocal(target, LocalPlan with { Areas = areas });
        }

        draftPolygonId = null;
        EditMode = FenceMapEditMode.None;
        StatusMessage = index >= 0 && areas[index].Vertices.Count >= 3
            ? "Polygon closed locally."
            : "Polygon closed but remains invalid until it has at least three vertices.";
    }

    [RelayCommand]
    private void CancelEdit()
    {
        if (vehicleId is { } target && draftPolygonId is { } draft)
        {
            UpdateLocal(target, LocalPlan with { Areas = LocalPlan.Areas.Where(area => area.Id != draft).ToArray() });
        }

        draftPolygonId = null;
        EditMode = FenceMapEditMode.None;
        StatusMessage = "Fence map editing cancelled.";
    }

    [RelayCommand]
    private void RemoveArea(FenceAreaListItem? item)
    {
        if (vehicleId is not { } target || item is null)
        {
            return;
        }

        UpdateLocal(target, LocalPlan with { Areas = LocalPlan.Areas.Where(area => area.Id != item.Id).ToArray() });
        SelectedArea = null;
        StatusMessage = "Fence area removed locally.";
    }

    [RelayCommand]
    private void RemoveReturnPoint()
    {
        if (vehicleId is { } target)
        {
            UpdateLocal(target, LocalPlan with { ReturnPoint = null });
            StatusMessage = "Fence return point removed locally.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanGeometryTransfer))]
    private async Task DownloadAsync()
    {
        if (vehicleId is not { } target)
        {
            return;
        }

        if (IsGeometryDirty && !await confirmation.ConfirmAsync(
                "Replace local fence",
                "Downloading will replace local fence edits. A backup will be retained.",
                "Download and replace"))
        {
            return;
        }

        await RunAsync(async token =>
        {
            var report = await fenceService.DownloadAsync(target, true, Progress(), token);
            ApplyReport(report);
        });
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task UploadAsync()
    {
        if (vehicleId is not { } target || parameterSession is null)
        {
            return;
        }

        if (!await confirmation.ConfirmAsync(
                "Replace vehicle fence",
                "Validated local geometry and pending fence parameters will replace the vehicle fence. The previous synchronized plan will be retained as a backup.",
                "Validate and upload"))
        {
            return;
        }

        await RunAsync(async token =>
        {
            var report = await fenceService.ApplyAsync(target, parameterSession, Progress(), token);
            ApplyReport(report);
        });
    }

    [RelayCommand(CanExecute = nameof(CanGeometryTransfer))]
    private async Task ClearVehicleAsync()
    {
        if (vehicleId is not { } target || !await confirmation.ConfirmAsync(
                "Clear vehicle fence",
                "This removes all fence geometry from the vehicle after saving a local backup.",
                "Clear fence"))
        {
            return;
        }

        await RunAsync(async token => ApplyReport(await fenceService.ClearAsync(target, token)));
    }

    [RelayCommand(CanExecute = nameof(HasBackup))]
    private void RestoreBackup()
    {
        if (vehicleId is { } target)
        {
            ApplySnapshot(fenceService.RestoreBackup(target));
            StatusMessage = "The saved fence backup was restored locally; upload to apply it to the vehicle.";
        }
    }

    [RelayCommand]
    private void RevertParameters()
    {
        parameterSession?.RevertAll();
        SyncParameterRows();
        StatusMessage = "Pending fence parameter edits were reverted to live values.";
    }

    [RelayCommand]
    private async Task RefreshParametersAsync()
    {
        if (parameterSession is null)
        {
            return;
        }

        await RunAsync(async token =>
        {
            await parameterSession.RefreshAsync(cancellationToken: token);
            StatusMessage = "Fence parameter refresh requests were sent.";
        });
    }

    private bool CanOperate() => IsConnected && !IsBusy;

    private bool CanGeometryTransfer() => CanOperate() && SupportsTypedGeometry;

    private void BeginPolygon(FenceMapEditMode mode)
    {
        CancelDraftWithoutStatus();
        EditMode = mode;
        StatusMessage = "Click the map to add polygon vertices, then choose Finish polygon.";
    }

    private void CancelDraftWithoutStatus()
    {
        if (vehicleId is { } target && draftPolygonId is { } draft)
        {
            UpdateLocal(target, LocalPlan with { Areas = LocalPlan.Areas.Where(area => area.Id != draft).ToArray() });
        }

        draftPolygonId = null;
    }

    private async Task InitializeForActiveVehicleAsync()
    {
        SupersedeOperation();
        var snapshot = activeVehicle.Current;
        IsConnected = snapshot.IsOnline;
        if (!snapshot.IsOnline || snapshot.VehicleId is not { } target)
        {
            vehicleId = null;
            SupportsTypedGeometry = false;
            DetachParameterSession();
            Parameters.Clear();
            StatusMessage = "Connect a vehicle to configure GeoFence.";
            NotifyTransferCommands();
            return;
        }

        vehicleId = target;
        ApplySnapshot(fenceService.GetSnapshot(target));
        SupportsTypedGeometry = fenceService.SupportsTypedGeometry(target);
        NotifyTransferCommands();
        await RunAsync(async token =>
        {
            DetachParameterSession();
            parameterSession = await fenceService.OpenParameterSessionAsync(target, token);
            parameterSession.Changed += OnParameterSessionChanged;
            SyncParameterRows();
            StatusMessage = SupportsTypedGeometry
                ? "Fence parameters loaded. Download geometry or begin local fence editing."
                : "Fence parameters loaded, but this firmware does not advertise typed fence geometry.";
        }, replaceRunning: true);
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args) => _ = InitializeForActiveVehicleAsync();

    private void OnParameterSessionChanged(object? sender, EventArgs args) => SyncParameterRows();

    private void SyncParameterRows()
    {
        if (parameterSession is null)
        {
            return;
        }

        var fields = parameterSession.Fields
            .Where(field => fenceService.ParameterDefinitions.Any(definition => definition.ParameterNames.Contains(field.Name, StringComparer.Ordinal)))
            .ToArray();
        foreach (var field in fields)
        {
            var item = Parameters.FirstOrDefault(candidate => candidate.Name == field.Name);
            if (item is null)
            {
                Parameters.Add(new ParameterItemViewModel(parameterSession, field));
            }
            else
            {
                item.SetField(field);
            }
        }

        foreach (var stale in Parameters.Where(item => fields.All(field => field.Name != item.Name)).ToArray())
        {
            Parameters.Remove(stale);
        }
    }

    private void UpdateLocal(VehicleId target, FencePlan plan) => ApplySnapshot(fenceService.SetLocalPlan(target, plan));

    private void ApplyReport(FenceOperationReport report)
    {
        ApplySnapshot(report.Snapshot);
        StatusMessage = report.Validation.IsValid
            ? report.Message
            : string.Join(Environment.NewLine, report.Validation.Issues.Select(issue => issue.Message));
    }

    private void ApplySnapshot(FenceConfigurationSnapshot snapshot)
    {
        LocalPlan = snapshot.LocalPlan;
        Areas.Clear();
        foreach (var area in snapshot.LocalPlan.Areas)
        {
            var summary = area.Kind is FenceAreaKind.PolygonInclusion or FenceAreaKind.PolygonExclusion
                ? $"{area.Vertices.Count} vertices{(area.IsClosed ? string.Empty : " (open)")}"
                : $"{area.RadiusMeters:0.#} m radius";
            Areas.Add(new FenceAreaListItem(area.Id, area.Kind, summary, area.IsClosed));
        }

        IsGeometryDirty = snapshot.IsDirty;
        HasBackup = snapshot.BackupPlan is not null;
        LocalRevisionText = $"Local revision {snapshot.LocalRevision}";
        VehicleRevisionText = snapshot.VehicleRevision is { } revision
            ? $"Vehicle revision {revision}"
            : "Vehicle revision not downloaded";
        ReturnPointText = snapshot.LocalPlan.ReturnPoint is { } point
            ? $"Return: {point.LatitudeDegrees:F6}, {point.LongitudeDegrees:F6}"
            : "Return point not set";
        GeometryChanged?.Invoke(this, EventArgs.Empty);
    }

    private IProgress<FenceTransferProgress> Progress() => new Progress<FenceTransferProgress>(value =>
    {
        TransferPercent = value.Total <= 0 ? 0 : value.Completed / (double)value.Total;
        StatusMessage = $"{value.Stage}: {value.Completed}/{value.Total}";
    });

    private async Task RunAsync(Func<CancellationToken, Task> operation, bool replaceRunning = false)
    {
        if (IsBusy && !replaceRunning)
        {
            return;
        }

        if (replaceRunning)
        {
            SupersedeOperation();
        }

        var cancellation = new CancellationTokenSource();
        operationCancellation = cancellation;
        IsBusy = true;
        NotifyTransferCommands();
        try
        {
            await operation(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(operationCancellation, cancellation))
            {
                StatusMessage = "Fence operation cancelled.";
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "GeoFence operation failed for {VehicleId}.", vehicleId);
            if (ReferenceEquals(operationCancellation, cancellation))
            {
                StatusMessage = $"Fence operation failed: {exception.Message}";
            }
        }
        finally
        {
            if (ReferenceEquals(operationCancellation, cancellation))
            {
                operationCancellation = null;
                IsBusy = false;
                NotifyTransferCommands();
            }

            cancellation.Dispose();
        }
    }

    private void CancelOperation()
    {
        operationCancellation?.Cancel();
    }

    private void SupersedeOperation()
    {
        var previous = operationCancellation;
        operationCancellation = null;
        previous?.Cancel();
        IsBusy = false;
        NotifyTransferCommands();
    }

    private void DetachParameterSession()
    {
        if (parameterSession is not null)
        {
            parameterSession.Changed -= OnParameterSessionChanged;
            parameterSession = null;
        }
    }

    private void NotifyTransferCommands()
    {
        DownloadCommand.NotifyCanExecuteChanged();
        UploadCommand.NotifyCanExecuteChanged();
        ClearVehicleCommand.NotifyCanExecuteChanged();
    }
}
