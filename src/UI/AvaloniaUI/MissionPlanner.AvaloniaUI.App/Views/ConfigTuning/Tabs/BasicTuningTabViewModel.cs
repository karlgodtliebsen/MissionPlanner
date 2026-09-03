using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Presentation;
using MissionPlanner.Core.ConfigTuning.Tuning;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Models;

namespace MissionPlanner.AvaloniaUI.App.Views.ConfigTuning.Tabs;

/// <summary>Coordinates the active-vehicle Basic Tuning workspace and file operations.</summary>
public sealed partial class BasicTuningTabViewModel : ViewModelBase
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IBasicTuningService tuningService;
    private readonly ParametersFileHandler fileHandler;
    private readonly IUserConfirmationService confirmation;
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
    /// <param name="dispatcher">The UI Dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public BasicTuningTabViewModel(
        IActiveVehicleContext activeVehicle,
        IBasicTuningService tuningService,
        ParametersFileHandler fileHandler,
        IUserConfirmationService confirmation, ILogger<BasicTuningTabViewModel> logger) : base(logger)
    {
        this.activeVehicle = activeVehicle;
        this.tuningService = tuningService;
        this.fileHandler = fileHandler;
        this.confirmation = confirmation;
        SetMessages("Connect a vehicle to use Basic Tuning.");
    }

    /// <summary>Gets the firmware-supported tuning groups.</summary>
    public ObservableCollection<BasicTuningGroupViewModel> Groups
    {
        get;
    } = [];

    /// <summary>Gets whether the target vehicle is connected.</summary>
    [ObservableProperty]
    public partial bool IsConnected
    {
        get;
        private set;
    }

    /// <summary>Gets whether the firmware has a Basic Tuning profile with supported fields.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnsupported))]
    public partial bool HasSupportedProfile
    {
        get;
        private set;
    }

    /// <summary>Gets whether no Basic Tuning profile is available.</summary>
    public bool IsUnsupported => IsConnected && !HasSupportedProfile;

    /// <summary>Gets whether an operation is running.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand), nameof(ExportCommand))]
    public new partial bool IsBusy
    {
        get;
        private set;
    }

    /// <summary>Gets the connected firmware-family label.</summary>
    [ObservableProperty]
    public partial string FirmwareFamilyText
    {
        get;
        private set;
    } = "No vehicle connected";


    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Deactivate();
        disposed = true;
        DetachWorkspace();
        base.Dispose();
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
        Dispatcher.Dispatch(() => _ = InitializeAsync());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        Deactivate();
        return Task.CompletedTask;
    }

    private void Deactivate()
    {

        if (!active)
        {
            return;
        }

        active = false;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        CancelOperation();
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
            SetMessages(path is null ? "Basic Tuning export was cancelled." : $"Basic Tuning exported to {path}.");
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
                SetMessages("Basic Tuning import was cancelled.");
                return;
            }

            var result = tuningService.Import(workspace, content);
            RefreshGroups();
            SetMessages(result.Success
                ? $"Imported {result.ImportedCount} presented tuning values; {result.IgnoredNames.Count} unsupported names ignored. Review and apply each group."
                : string.Join(" ", result.Errors));
        }).ConfigureAwait(false);
    }

    private bool CanOperate()
    {
        return !IsBusy && workspace is not null && workspace.Session.IsValid;
    }

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
                SetMessages("Tuning changes were not applied.");
                return;
            }

            var result = await tuningService.ApplyGroupAsync(workspace, group.Key, cancellationToken);
            group.ValidationMessage = result.ValidationIssues.Count == 0
                ? null
                : string.Join(" ", result.ValidationIssues.Select(issue => issue.Message));
            RefreshGroups();
            SetMessages(result.Success
                ? result.ParameterReport?.RebootRequired == true
                    ? $"{group.Title} applied and confirmed. Vehicle Reboot is required for one or more changes."
                    : $"{group.Title} applied and confirmed."
                : result.ValidationIssues.Count > 0
                    ? group.ValidationMessage!
                    : $"{group.Title} was not fully confirmed; failed fields remain pending.");
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
        SetMessages($"Pending changes in {group.Title} were reverted to live values.");
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
            SetMessages($"Refresh requested for {group.Title}.");
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
            SetMessages("Connect a vehicle to use Basic Tuning.");
            return;
        }

        await RunAsync(async cancellationToken =>
        {
            workspace = await tuningService.OpenAsync(vehicleId, cancellationToken);
            if (workspace is null || workspace.Groups.Count == 0)
            {
                SetMessages($"No curated Basic Tuning fields are available for {FirmwareFamilyText}.");
                return;
            }

            workspace.Session.Changed += OnSessionChanged;
            foreach (var group in workspace.Groups)
            {
                Groups.Add(new BasicTuningGroupViewModel(group, workspace.Session, ApplyGroupAsync, RevertGroup, RefreshGroupAsync));
            }

            HasSupportedProfile = true;
            SetMessages($"Loaded {Groups.Sum(group => group.Parameters.Count)} supported fields for {FirmwareFamilyText}. Changes are applied one group at a time.");
        }).ConfigureAwait(false);
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        CancelOperation();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        operationCancellation = cancellation;
        SetBusy();
        try
        {
            await operation(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            SetMessages(activeVehicle.IsOnline ? "Basic Tuning operation cancelled." : "Vehicle disconnected; Basic Tuning operation cancelled.");
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Basic Tuning operation failed.");
            SetMessages(exception);
        }
        finally
        {
            if (ReferenceEquals(operationCancellation, cancellation))
            {
                operationCancellation = null;
                ResetBusy();
            }

            ImportCommand.NotifyCanExecuteChanged();
            ExportCommand.NotifyCanExecuteChanged();
        }
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        var next = ActiveProfileKey.From(args.Current);
        if (next == activeKey)
        {
            return;
        }

        Dispatcher.Dispatch(() => _ = InitializeAsync());
    }

    private void OnSessionChanged()
    {
        Dispatcher.Dispatch(() =>
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
        workspace?.Session.Changed -= OnSessionChanged;
        workspace = null;
    }

    private void CancelOperation()
    {
        operationCancellation?.Cancel();
        operationCancellation = null;
        ResetBusy();
    }

    private readonly record struct ActiveProfileKey(VehicleId? VehicleId, bool IsOnline, VehicleFirmwareIdentity? Firmware)
    {
        public static ActiveProfileKey From(ActiveVehicleSnapshot snapshot)
        {
            return new ActiveProfileKey(snapshot.VehicleId, snapshot.IsOnline, snapshot.State?.Identity.Firmware);
        }
    }
}

