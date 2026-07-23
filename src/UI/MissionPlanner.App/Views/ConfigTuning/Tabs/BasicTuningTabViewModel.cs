using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Tuning;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Projects one curated tuning field and its shared-session editor.</summary>
public sealed class BasicTuningParameterViewModel
{
    /// <summary>Initializes a curated tuning field projection.</summary>
    /// <param name="resolved">The resolved field definition.</param>
    /// <param name="session">The shared parameter session.</param>
    public BasicTuningParameterViewModel(ResolvedBasicTuningField resolved, IParameterEditSession session)
    {
        Definition = resolved.Definition;
        ParameterName = resolved.ParameterName;
        Editor = new ParameterItemViewModel(session, session.GetField(ParameterName)!);
    }

    /// <summary>Gets the curated field definition.</summary>
    public BasicTuningFieldDefinition Definition { get; }

    /// <summary>Gets the resolved vehicle parameter name.</summary>
    public string ParameterName { get; }

    /// <summary>Gets the shared-session parameter editor.</summary>
    public ParameterItemViewModel Editor { get; }

    /// <summary>Gets the plain-language title.</summary>
    public string Title => Definition.Title;

    /// <summary>Gets the plain-language explanation.</summary>
    public string Description => Definition.Description;

    /// <summary>Gets metadata units with a curated fallback.</summary>
    public string Units => string.IsNullOrWhiteSpace(Editor.Units) ? Definition.FallbackUnits : Editor.Units;

    /// <summary>Gets the optional field-level stability warning.</summary>
    public string? Warning => Definition.Warning;

    /// <summary>Gets whether a field-level warning is present.</summary>
    public bool HasWarning => !string.IsNullOrWhiteSpace(Warning);

    /// <summary>Gets whether an authoritative recommendation can be shown.</summary>
    public bool HasRecommendation => Definition.HasAuthoritativeRecommendation;

    /// <summary>Gets the sourced recommendation text.</summary>
    public string RecommendationText => HasRecommendation
        ? $"Recommended: {Definition.RecommendedValue} {Units} ({Definition.RecommendationSource})"
        : string.Empty;

    /// <summary>Refreshes the editor projection from the shared session.</summary>
    /// <param name="session">The shared parameter session.</param>
    public void Refresh(IParameterEditSession session)
    {
        if (session.GetField(ParameterName) is { } field)
        {
            Editor.SetField(field);
        }
    }
}

/// <summary>Projects one firmware-curated Basic Tuning group and its scoped actions.</summary>
public sealed partial class BasicTuningGroupViewModel : ObservableObject
{
    private readonly IParameterEditSession session;

    /// <summary>Initializes a group projection.</summary>
    /// <param name="group">The resolved group.</param>
    /// <param name="session">The shared editing session.</param>
    /// <param name="apply">The group apply callback.</param>
    /// <param name="revert">The group revert callback.</param>
    /// <param name="refresh">The group refresh callback.</param>
    public BasicTuningGroupViewModel(
        ResolvedBasicTuningGroup group,
        IParameterEditSession session,
        Func<BasicTuningGroupViewModel, Task> apply,
        Action<BasicTuningGroupViewModel> revert,
        Func<BasicTuningGroupViewModel, Task> refresh)
    {
        Definition = group.Definition;
        this.session = session;
        Parameters = new ObservableCollection<BasicTuningParameterViewModel>(
            group.Fields.Select(item => new BasicTuningParameterViewModel(item, session)));
        ApplyCommand = new AsyncRelayCommand(() => apply(this));
        RevertCommand = new RelayCommand(() => revert(this));
        RefreshCommand = new AsyncRelayCommand(() => refresh(this));
        Refresh();
    }

    /// <summary>Gets the curated group definition.</summary>
    public BasicTuningGroupDefinition Definition { get; }

    /// <summary>Gets the stable group key.</summary>
    public string Key => Definition.Key;

    /// <summary>Gets the group title.</summary>
    public string Title => Definition.Title;

    /// <summary>Gets the group description.</summary>
    public string Description => Definition.Description;

    /// <summary>Gets the optional stability warning.</summary>
    public string? Warning => Definition.Warning;

    /// <summary>Gets whether a group stability warning is present.</summary>
    public bool HasWarning => !string.IsNullOrWhiteSpace(Warning);

    /// <summary>Gets the displayed tuning fields.</summary>
    public ObservableCollection<BasicTuningParameterViewModel> Parameters { get; }

    /// <summary>Gets the command that validates and applies this group.</summary>
    public IAsyncRelayCommand ApplyCommand { get; }

    /// <summary>Gets the command that reverts this group.</summary>
    public IRelayCommand RevertCommand { get; }

    /// <summary>Gets the command that refreshes this group.</summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>Gets whether this group contains pending changes.</summary>
    [ObservableProperty]
    public partial bool IsModified { get; private set; }

    /// <summary>Gets the latest group validation message.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    public partial string? ValidationMessage { get; set; }

    /// <summary>Gets whether coupled group validation currently fails.</summary>
    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

    /// <summary>Refreshes all editor values and derived state.</summary>
    public void Refresh()
    {
        foreach (var parameter in Parameters)
        {
            parameter.Refresh(session);
        }

        IsModified = Parameters.Any(parameter => parameter.Editor.IsModified);
    }
}

/// <summary>Coordinates the active-vehicle Basic Tuning workspace and file operations.</summary>
public sealed partial class BasicTuningTabViewModel : ObservableObject, IDisposable
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IBasicTuningService tuningService;
    private readonly ParametersFileHandler fileHandler;
    private readonly IUserConfirmationService confirmation;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<BasicTuningTabViewModel> logger;
    private BasicTuningWorkspace? workspace;
    private CancellationTokenSource? operationCancellation;
    private ActiveProfileKey activeKey;
    private bool active;
    private bool disposed;

    /// <summary>Initializes the Basic Tuning page.</summary>
    /// <param name="activeVehicle">The active-vehicle context.</param>
    /// <param name="tuningService">The curated tuning service.</param>
    /// <param name="fileHandler">The Config file helper.</param>
    /// <param name="confirmation">The hazardous-change confirmation service.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public BasicTuningTabViewModel(
        IActiveVehicleContext activeVehicle,
        IBasicTuningService tuningService,
        ParametersFileHandler fileHandler,
        IUserConfirmationService confirmation,
        IDispatcher dispatcher,
        ILogger<BasicTuningTabViewModel> logger)
    {
        this.activeVehicle = activeVehicle;
        this.tuningService = tuningService;
        this.fileHandler = fileHandler;
        this.confirmation = confirmation;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    /// <summary>Gets the firmware-supported tuning groups.</summary>
    public ObservableCollection<BasicTuningGroupViewModel> Groups { get; } = [];

    /// <summary>Gets whether the target vehicle is connected.</summary>
    [ObservableProperty]
    public partial bool IsConnected { get; private set; }

    /// <summary>Gets whether the firmware has a Basic Tuning profile with supported fields.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnsupported))]
    public partial bool HasSupportedProfile { get; private set; }

    /// <summary>Gets whether no Basic Tuning profile is available.</summary>
    public bool IsUnsupported => IsConnected && !HasSupportedProfile;

    /// <summary>Gets whether an operation is running.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand), nameof(ExportCommand))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Gets the connected firmware-family label.</summary>
    [ObservableProperty]
    public partial string FirmwareFamilyText { get; private set; } = "No vehicle connected";

    /// <summary>Gets the latest lifecycle, validation, file, or write status.</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; private set; } = "Connect a vehicle to use Basic Tuning.";

    /// <summary>Activates active-vehicle observation and opens the supported profile.</summary>
    public void Activate()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (active)
        {
            return;
        }

        active = true;
        activeVehicle.Changed += OnActiveVehicleChanged;
        dispatcher.Dispatch(() => _ = InitializeAsync());
    }

    /// <summary>Stops active-vehicle observation and cancels current work.</summary>
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

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ExportAsync()
    {
        if (workspace is null)
        {
            return;
        }

        await RunAsync(async cancellationToken =>
        {
            var content = tuningService.Export(workspace);
            var path = await fileHandler.SaveTextFileAsync(
                $"basic-tuning-{workspace.Profile.Family}.json",
                content,
                cancellationToken);
            StatusMessage = path is null ? "Basic Tuning export was cancelled." : $"Basic Tuning exported to {path}.";
        }).ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanOperate))]
    private async Task ImportAsync()
    {
        if (workspace is null)
        {
            return;
        }

        await RunAsync(async cancellationToken =>
        {
            var content = await fileHandler.LoadTextFileAsync("Select a Basic Tuning JSON file", cancellationToken);
            if (content is null)
            {
                StatusMessage = "Basic Tuning import was cancelled.";
                return;
            }

            var result = tuningService.Import(workspace, content);
            RefreshGroups();
            StatusMessage = result.Success
                ? $"Imported {result.ImportedCount} presented tuning values; {result.IgnoredNames.Count} unsupported names ignored. Review and apply each group."
                : string.Join(" ", result.Errors);
        }).ConfigureAwait(false);
    }

    private bool CanOperate() => !IsBusy && workspace is not null && workspace.Session.IsValid;

    private async Task ApplyGroupAsync(BasicTuningGroupViewModel group)
    {
        if (workspace is null || IsBusy)
        {
            return;
        }

        await RunAsync(async cancellationToken =>
        {
            if (group.HasWarning && !await confirmation.ConfirmAsync(
                    "Apply tuning changes?",
                    $"{group.Warning}\n\nApply pending changes in {group.Title}?",
                    "Apply",
                    cancellationToken))
            {
                StatusMessage = "Tuning changes were not applied.";
                return;
            }

            var result = await tuningService.ApplyGroupAsync(workspace, group.Key, cancellationToken);
            group.ValidationMessage = result.ValidationIssues.Count == 0
                ? null
                : string.Join(" ", result.ValidationIssues.Select(issue => issue.Message));
            RefreshGroups();
            StatusMessage = result.Success
                ? result.ParameterReport?.RebootRequired == true
                    ? $"{group.Title} applied and confirmed. Reboot is required for one or more changes."
                    : $"{group.Title} applied and confirmed."
                : result.ValidationIssues.Count > 0
                    ? group.ValidationMessage!
                    : $"{group.Title} was not fully confirmed; failed fields remain pending.";
        }).ConfigureAwait(false);
    }

    private void RevertGroup(BasicTuningGroupViewModel group)
    {
        if (workspace is null || IsBusy)
        {
            return;
        }

        tuningService.RevertGroup(workspace, group.Key);
        group.ValidationMessage = null;
        RefreshGroups();
        StatusMessage = $"Pending changes in {group.Title} were reverted to live values.";
    }

    private async Task RefreshGroupAsync(BasicTuningGroupViewModel group)
    {
        if (workspace is null || IsBusy)
        {
            return;
        }

        await RunAsync(async cancellationToken =>
        {
            await workspace.Session.RefreshAsync(
                group.Parameters.Select(parameter => parameter.ParameterName).ToArray(),
                cancellationToken);
            RefreshGroups();
            StatusMessage = $"Refresh requested for {group.Title}.";
        }).ConfigureAwait(false);
    }

    private async Task InitializeAsync()
    {
        CancelOperation();
        DetachWorkspace();
        Groups.Clear();
        var snapshot = activeVehicle.Current;
        activeKey = ActiveProfileKey.From(snapshot);
        IsConnected = snapshot.IsOnline;
        FirmwareFamilyText = snapshot.State?.Identity.Firmware.Family.ToString() ?? "No vehicle connected";
        HasSupportedProfile = false;
        if (!snapshot.IsOnline || snapshot.VehicleId is not { } vehicleId)
        {
            StatusMessage = "Connect a vehicle to use Basic Tuning.";
            return;
        }

        await RunAsync(async cancellationToken =>
        {
            workspace = await tuningService.OpenAsync(vehicleId, cancellationToken);
            if (workspace is null || workspace.Groups.Count == 0)
            {
                StatusMessage = $"No curated Basic Tuning fields are available for {FirmwareFamilyText}.";
                return;
            }

            workspace.Session.Changed += OnSessionChanged;
            foreach (var group in workspace.Groups)
            {
                Groups.Add(new BasicTuningGroupViewModel(group, workspace.Session, ApplyGroupAsync, RevertGroup, RefreshGroupAsync));
            }

            HasSupportedProfile = true;
            StatusMessage = $"Loaded {Groups.Sum(group => group.Parameters.Count)} supported fields for {FirmwareFamilyText}. Changes are applied one group at a time.";
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
            StatusMessage = activeVehicle.IsOnline ? "Basic Tuning operation cancelled." : "Vehicle disconnected; Basic Tuning operation cancelled.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Basic Tuning operation failed.");
            StatusMessage = exception.Message;
        }
        finally
        {
            if (ReferenceEquals(operationCancellation, cancellation))
            {
                operationCancellation = null;
                IsBusy = false;
            }

            ImportCommand.NotifyCanExecuteChanged();
            ExportCommand.NotifyCanExecuteChanged();
        }
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        var next = ActiveProfileKey.From(args.Current);
        if (next == activeKey)
        {
            return;
        }

        dispatcher.Dispatch(() => _ = InitializeAsync());
    }

    private void OnSessionChanged(object? sender, EventArgs args)
    {
        dispatcher.Dispatch(() =>
        {
            RefreshGroups();
            foreach (var group in Groups)
            {
                if (workspace is not null)
                {
                    var issues = tuningService.ValidateGroup(workspace, group.Key);
                    group.ValidationMessage = issues.Count == 0 ? null : string.Join(" ", issues.Select(issue => issue.Message));
                }
            }
        });
    }

    private void RefreshGroups()
    {
        foreach (var group in Groups)
        {
            group.Refresh();
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

    private readonly record struct ActiveProfileKey(
        VehicleId? VehicleId,
        bool IsOnline,
        VehicleFirmwareIdentity? Firmware)
    {
        public static ActiveProfileKey From(ActiveVehicleSnapshot snapshot) =>
            new(snapshot.VehicleId, snapshot.IsOnline, snapshot.State?.Identity.Firmware);
    }
}
