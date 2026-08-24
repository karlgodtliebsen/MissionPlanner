using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.ConfigTuning.Osd;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Shared.Models.Vehicles.Models;
using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Coordinates onboard OSD discovery, placement preview, files, and confirmed writes.</summary>
public sealed partial class OnboardOsdTabViewModel : BaseViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IOsdConfigurationService osdService;
    private readonly ParametersFileHandler fileHandler;
    private readonly IUserConfirmationService confirmation;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<OnboardOsdTabViewModel> logger;
    private OsdConfigurationWorkspace? workspace;
    private CancellationTokenSource? operationCancellation;
    private ActiveProfileKey activeKey;
    private bool active;
    private bool disposed;

    /// <summary>Initializes the onboard OSD page.</summary>
    /// <param name="activeVehicle">The active-vehicle context.</param>
    /// <param name="osdService">The OSD configuration service.</param>
    /// <param name="fileHandler">The Config file helper.</param>
    /// <param name="confirmation">The overlap/write confirmation service.</param>
    /// <param name="dispatcher">The UI Dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public OnboardOsdTabViewModel(
        IActiveVehicleContext activeVehicle,
        IOsdConfigurationService osdService,
        ParametersFileHandler fileHandler,
        IUserConfirmationService confirmation,
        IDispatcher dispatcher,
        ILogger<OnboardOsdTabViewModel> logger) : base(logger)
    {
        this.activeVehicle = activeVehicle;
        this.osdService = osdService;
        this.fileHandler = fileHandler;
        this.confirmation = confirmation;
        this.dispatcher = dispatcher;
        this.logger = logger;
        StatusMessage = "Connect a vehicle to discover onboard OSD parameters.";
    }

    /// <summary>Gets discovered OSD screens.</summary>
    public ObservableCollection<OsdScreenViewModel> Screens
    {
        get;
    } = [];

    /// <summary>Gets firmware-global OSD parameters.</summary>
    public ObservableCollection<ParameterItemViewModel> GlobalParameters
    {
        get;
    } = [];

    /// <summary>Gets whether firmware-global OSD options were discovered.</summary>
    [ObservableProperty]
    public partial bool HasGlobalParameters
    {
        get;
        private set;
    }

    /// <summary>Gets preview items for the selected screen.</summary>
    public ObservableRangeCollection<OsdPreviewItem> PreviewItems
    {
        get;
    } = [];

    /// <summary>Gets or sets the selected screen.</summary>
    [ObservableProperty]
    public partial OsdScreenViewModel? SelectedScreen
    {
        get;
        set;
    }

    /// <summary>Gets or sets the selected item.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveLeftCommand), nameof(MoveRightCommand), nameof(MoveUpCommand), nameof(MoveDownCommand))]
    public partial OsdItemViewModel? SelectedItem
    {
        get;
        set;
    }

    /// <summary>Gets whether a vehicle is connected.</summary>
    [ObservableProperty]
    public partial bool IsConnected
    {
        get;
        private set;
    }

    /// <summary>Gets whether OSD parameters were discovered.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnsupported))]
    public partial bool HasOsdConfiguration
    {
        get;
        private set;
    }

    /// <summary>Gets whether no OSD configuration is present on the connected firmware.</summary>
    public bool IsUnsupported => IsConnected && !HasOsdConfiguration;

    /// <summary>Gets whether a file or write operation is active.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyScreenCommand), nameof(ResetScreenCommand), nameof(ImportCommand), nameof(ExportCommand))]
    public override partial bool IsBusy
    {
        get;
        set;
    }

    /// <summary>Gets the selected screen's grid width.</summary>
    [ObservableProperty]
    public partial int PreviewGridWidth
    {
        get;
        private set;
    } = 30;

    /// <summary>Gets the selected screen's grid height.</summary>
    [ObservableProperty]
    public partial int PreviewGridHeight
    {
        get;
        private set;
    } = 16;


    /// <summary>Gets current validation messages.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationIssues))]
    public partial string ValidationMessage
    {
        get;
        private set;
    } = string.Empty;

    /// <summary>Gets whether the selected screen has validation issues.</summary>
    public bool HasValidationIssues => !string.IsNullOrWhiteSpace(ValidationMessage);

    /// <summary>Occurs when the graphics preview should redraw.</summary>
    public event EventHandler? LayoutChanged;

    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        DeactivateAsync().GetAwaiter().GetResult();
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
        InitializeAsync().GetAwaiter().GetResult();
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
        CancelOperation();
        return Task.CompletedTask;
    }

    partial void OnSelectedScreenChanged(OsdScreenViewModel? value)
    {
        SelectedItem = value?.Items.FirstOrDefault();
        PreviewGridWidth = value?.Definition.GridWidth ?? 30;
        PreviewGridHeight = value?.Definition.GridHeight ?? 16;
        RefreshPreviewAndValidation();
        ApplyScreenCommand.NotifyCanExecuteChanged();
        ResetScreenCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedItemChanged(OsdItemViewModel? value)
    {
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanUseScreen))]
    private async Task ApplyScreenAsync()
    {
        if (workspace is null || SelectedScreen is null)
        {
            return;
        }

        await RunAsync(async cancellationToken =>
        {
            var issues = osdService.ValidateScreen(workspace, SelectedScreen.Number);
            if (issues.Any(issue => issue.Severity == OsdValidationSeverity.Error))
            {
                RefreshPreviewAndValidation();
                StatusMessage = "Invalid OSD coordinates or unsupported overlaps must be corrected before apply.";
                return;
            }

            var allowWarnings = false;
            if (issues.Any(issue => issue.Severity == OsdValidationSeverity.Warning))
            {
                allowWarnings = await confirmation.ConfirmAsync(
                    "Apply dynamic OSD overlap?",
                    string.Join(Environment.NewLine, issues.Select(issue => issue.Message)),
                    "Apply overlap",
                    cancellationToken);
                if (!allowWarnings)
                {
                    StatusMessage = "OSD overlap was not applied.";
                    return;
                }
            }

            var result = await osdService.ApplyScreenAsync(
                workspace,
                SelectedScreen.Number,
                allowWarnings,
                cancellationToken);
            RefreshAll();
            StatusMessage = result.Success
                ? $"{SelectedScreen.Title} changes applied and confirmed."
                : $"{SelectedScreen.Title} was not fully confirmed; failed values remain pending.";
        }).ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanUseScreen))]
    private void ResetScreen()
    {
        if (workspace is null || SelectedScreen is null)
        {
            return;
        }

        osdService.ResetScreen(workspace, SelectedScreen.Number);
        RefreshAll();
        StatusMessage = $"{SelectedScreen.Title} reset to current live vehicle values.";
    }

    [RelayCommand(CanExecute = nameof(CanUseWorkspace))]
    private async Task ExportAsync()
    {
        if (workspace is null)
        {
            return;
        }

        await RunAsync(async cancellationToken =>
        {
            var path = await fileHandler.SaveTextFileAsync(
                $"onboard-osd-{workspace.Scope.FirmwareIdentity.Family}.json",
                osdService.Export(workspace),
                cancellationToken);
            StatusMessage = path is null ? "OSD export was cancelled." : $"OSD layout exported to {path}.";
        }).ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanUseWorkspace))]
    private async Task ImportAsync()
    {
        if (workspace is null)
        {
            return;
        }

        await RunAsync(async cancellationToken =>
        {
            var content = await fileHandler.LoadTextFileAsync("Select an onboard OSD layout", cancellationToken);
            if (content is null)
            {
                StatusMessage = "OSD import was cancelled.";
                return;
            }

            var result = osdService.Import(workspace, content);
            RefreshAll();
            StatusMessage = result.Success
                ? $"Imported {result.ImportedCount} OSD values; {result.IgnoredNames.Count} unsupported names ignored. Review each screen before apply."
                : string.Join(" ", result.Errors.Concat(result.Issues.Select(issue => issue.Message)));
        }).ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanMove))]
    private void MoveLeft()
    {
        MoveSelected(-1, 0);
    }

    [RelayCommand(CanExecute = nameof(CanMove))]
    private void MoveRight()
    {
        MoveSelected(1, 0);
    }

    [RelayCommand(CanExecute = nameof(CanMove))]
    private void MoveUp()
    {
        MoveSelected(0, -1);
    }

    [RelayCommand(CanExecute = nameof(CanMove))]
    private void MoveDown()
    {
        MoveSelected(0, 1);
    }

    private bool CanUseWorkspace()
    {
        return !IsBusy && workspace?.Session.IsValid == true;
    }

    private bool CanUseScreen()
    {
        return CanUseWorkspace() && SelectedScreen is not null;
    }

    private bool CanMove()
    {
        return CanUseScreen() && SelectedItem is not null;
    }

    private void MoveSelected(int deltaColumn, int deltaRow)
    {
        if (SelectedItem is null)
        {
            return;
        }

        MoveItem(SelectedItem, SelectedItem.Column + deltaColumn, SelectedItem.Row + deltaRow);
        RefreshAll();
    }

    private string? MoveItem(OsdItemViewModel item, int column, int row)
    {
        if (workspace is null || SelectedScreen is null)
        {
            return "No OSD screen is selected.";
        }

        var error = osdService.MoveItem(workspace, SelectedScreen.Number, item.Key, column, row);
        RefreshPreviewAndValidation();
        return error;
    }

    private async Task InitializeAsync()
    {
        CancelOperation();
        DetachWorkspace();
        Screens.Clear();
        GlobalParameters.Clear();
        HasGlobalParameters = false;
        PreviewItems.Clear();
        SelectedScreen = null;
        var snapshot = activeVehicle.Current;
        activeKey = ActiveProfileKey.From(snapshot);
        IsConnected = snapshot.IsOnline;
        HasOsdConfiguration = false;
        if (!snapshot.IsOnline || snapshot.VehicleId is not { } vehicleId)
        {
            StatusMessage = "Connect a vehicle to discover onboard OSD parameters.";
            return;
        }

        await RunAsync(async cancellationToken =>
        {
            workspace = await osdService.OpenAsync(vehicleId, cancellationToken);
            if (workspace is null)
            {
                StatusMessage = "The connected firmware exposes no supported onboard OSD parameters.";
                return;
            }

            workspace.Session.Changed += OnSessionChanged;
            foreach (var name in workspace.GlobalParameterNames)
            {
                if (workspace.Session.GetField(name) is { } state)
                {
                    GlobalParameters.Add(new ParameterItemViewModel(workspace.Session, state));
                }
            }

            HasGlobalParameters = GlobalParameters.Count > 0;

            foreach (var screen in workspace.Screens)
            {
                Screens.Add(new OsdScreenViewModel(screen, workspace.Session, MoveItem));
            }

            HasOsdConfiguration = true;
            SelectedScreen = Screens.FirstOrDefault();
            StatusMessage = $"Discovered {Screens.Count} OSD screens and {Screens.Sum(screen => screen.Items.Count)} firmware-defined items.";
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
            StatusMessage = activeVehicle.IsOnline ? "OSD operation cancelled." : "Vehicle disconnected; OSD operation cancelled.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Onboard OSD operation failed.");
            StatusMessage = exception.Message;
        }
        finally
        {
            if (ReferenceEquals(operationCancellation, cancellation))
            {
                operationCancellation = null;
                IsBusy = false;
            }

            NotifyCommands();
        }
    }

    private void RefreshAll()
    {
        if (workspace is null)
        {
            return;
        }

        foreach (var parameter in GlobalParameters)
        {
            if (workspace.Session.GetField(parameter.Name) is { } state)
            {
                parameter.SetField(state);
            }
        }

        foreach (var screen in Screens)
        {
            foreach (var parameter in screen.Parameters)
            {
                if (workspace.Session.GetField(parameter.Name) is { } state)
                {
                    parameter.SetField(state);
                }
            }

            foreach (var item in screen.Items)
            {
                item.Refresh();
            }
        }

        RefreshPreviewAndValidation();
    }

    private void RefreshPreviewAndValidation()
    {
        if (workspace is null || SelectedScreen is null)
        {
            ValidationMessage = string.Empty;
            LayoutChanged?.Invoke(this, EventArgs.Empty);
            PreviewItems.Clear();
            return;
        }

        var previewItems = new List<OsdPreviewItem>();
        foreach (var item in osdService.GetPreviewItems(workspace, SelectedScreen.Number))
        {
            previewItems.Add(item);
        }

        PreviewItems.ReplaceRange(previewItems);


        var issues = osdService.ValidateScreen(workspace, SelectedScreen.Number);
        ValidationMessage = string.Join(Environment.NewLine, issues.Select(issue => itemPrefix(issue) + issue.Message));
        foreach (var item in SelectedScreen.Items)
        {
            item.SetValidationError(issues.FirstOrDefault(issue => issue.ItemKeys.Contains(item.Key, StringComparer.Ordinal))?.Message);
        }

        LayoutChanged?.Invoke(this, EventArgs.Empty);

        static string itemPrefix(OsdValidationIssue issue)
        {
            return issue.Severity == OsdValidationSeverity.Warning ? "Warning: " : "Error: ";
        }
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        if (ActiveProfileKey.From(args.Current) != activeKey)
        {
            dispatcher.Dispatch(() => _ = InitializeAsync());
        }
    }

    private void OnSessionChanged(object? sender, EventArgs args)
    {
        dispatcher.Dispatch(RefreshAll);
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

    private void NotifyCommands()
    {
        ApplyScreenCommand.NotifyCanExecuteChanged();
        ResetScreenCommand.NotifyCanExecuteChanged();
        ImportCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        MoveLeftCommand.NotifyCanExecuteChanged();
        MoveRightCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    private readonly record struct ActiveProfileKey(VehicleId? VehicleId, bool IsOnline, VehicleFirmwareIdentity? Firmware)
    {
        public static ActiveProfileKey From(ActiveVehicleSnapshot snapshot)
        {
            return new ActiveProfileKey(snapshot.VehicleId, snapshot.IsOnline, snapshot.State?.Identity.Firmware);
        }
    }
}
