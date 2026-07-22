using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNext.Collections.Generic;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Common;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Parameters;
using UraniumUI.Dialogs;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Provides the searchable full parameter list through the shared safe editing session.</summary>
public partial class FullParametersListTabViewModel : ObservableObject, IDisposable
{
    private readonly IVehicleConnectionSession connectionSession;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IParameterEditSessionFactory editSessionFactory;
    private readonly IDispatcher dispatcher;
    private readonly IDialogService dialogService;
    private readonly IExtendedDialogService extendedDialogService;
    private readonly IDomainFactory domainFactory;
    private readonly ParametersFileHandler parametersFileHandler;
    private readonly ILogger<FullParametersListTabViewModel> logger;
    private readonly List<ParameterItemViewModel> allParameterItems = [];
    private CancellationTokenSource? loadCancellation;
    private IParameterEditSession? editSession;
    private IDisposable? progressDialog;
    private bool active;
    private bool disposed;

    /// <summary>Initializes the Full Parameters List tab.</summary>
    /// <param name="connectionSession">The current connection-scoped services.</param>
    /// <param name="activeVehicle">The application active-vehicle context.</param>
    /// <param name="editSessionFactory">The shared parameter editing-session factory.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="dialogService">The dialog service.</param>
    /// <param name="extendedDialogService">The extended dialog service.</param>
    /// <param name="domainFactory">The domain view factory.</param>
    /// <param name="parametersFileHandler">The parameter import/export adapter.</param>
    /// <param name="logger">The logger.</param>
    public FullParametersListTabViewModel(
        IVehicleConnectionSession connectionSession,
        IActiveVehicleContext activeVehicle,
        IParameterEditSessionFactory editSessionFactory,
        IDispatcher dispatcher,
        IDialogService dialogService,
        IExtendedDialogService extendedDialogService,
        IDomainFactory domainFactory,
        ParametersFileHandler parametersFileHandler,
        ILogger<FullParametersListTabViewModel> logger)
    {
        this.connectionSession = connectionSession;
        this.activeVehicle = activeVehicle;
        this.editSessionFactory = editSessionFactory;
        this.dispatcher = dispatcher;
        this.dialogService = dialogService;
        this.extendedDialogService = extendedDialogService;
        this.domainFactory = domainFactory;
        this.parametersFileHandler = parametersFileHandler;
        this.logger = logger;
        HasConnection = activeVehicle.IsOnline;
    }

    /// <summary>Gets the currently visible parameter rows.</summary>
    public ObservableRangeCollection<ParameterItemViewModel> Parameters { get; } = [];

    /// <summary>Gets the current loading-progress message.</summary>
    [ObservableProperty]
    public partial string ProgressMessage { get; set; } = string.Empty;

    /// <summary>Gets whether the parameter grid is visible.</summary>
    [ObservableProperty]
    public partial bool ShowDataGrid { get; set; }

    /// <summary>Gets whether the current parameter view is empty.</summary>
    [ObservableProperty]
    public partial bool ShowEmptyView { get; set; } = true;

    /// <summary>Gets whether parameter loading is in progress.</summary>
    [ObservableProperty]
    public partial bool ShowLoadingProgress { get; set; }

    /// <summary>Gets whether the most recent load failed.</summary>
    [ObservableProperty]
    public partial bool ShowLoadingCompletedWithError { get; set; }

    /// <summary>Gets whether the most recent load was cancelled.</summary>
    [ObservableProperty]
    public partial bool ShowLoadingCancelled { get; set; }

    /// <summary>Gets whether the active vehicle is disconnected.</summary>
    [ObservableProperty]
    public partial bool ShowVehicleDisconnected { get; set; }

    /// <summary>Gets the number of unapplied parameter values.</summary>
    [ObservableProperty]
    public partial int ModifiedParameterCount { get; set; }

    /// <summary>Gets the total number of loaded parameter fields.</summary>
    [ObservableProperty]
    public partial int TotalParameterCount { get; set; }

    /// <summary>Gets or sets the parameter name and description filter.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Gets whether a load or apply operation is active.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Gets whether an active vehicle connection is available.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(WriteParametersCommand))]
    public partial bool HasConnection { get; set; }

    /// <summary>Gets the latest editing or apply status.</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Connect a vehicle, then refresh parameters.";

    /// <summary>Gets whether at least one confirmed change requires a vehicle reboot.</summary>
    [ObservableProperty]
    public partial bool RebootRequired { get; set; }

    /// <summary>Gets whether loading has completed and the parameter tools may be shown.</summary>
    public bool ShowLoadingCompleted => ShowDataGrid && !IsBusy;

    /// <summary>Activates vehicle lifecycle tracking while the tab is visible.</summary>
    public void Activate()
    {
        if (active || disposed)
        {
            return;
        }

        active = true;
        activeVehicle.Changed += OnActiveVehicleChanged;
        HasConnection = activeVehicle.IsOnline;
        ShowVehicleDisconnected = !HasConnection;
    }

    /// <summary>Deactivates lifecycle tracking and cancels the visible loading operation.</summary>
    public void Deactivate()
    {
        if (!active)
        {
            return;
        }

        active = false;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        CancelLoadOperation();
        CloseProgressDialog();
        IsBusy = false;
        ShowLoadingProgress = false;
    }

    partial void OnShowDataGridChanged(bool value) => OnPropertyChanged(nameof(ShowLoadingCompleted));

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowLoadingCompleted));
        RefreshParametersCommand.NotifyCanExecuteChanged();
        CancelLoadCommand.NotifyCanExecuteChanged();
        WriteParametersCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchTextChanged(string value) => FilterParameters();

    [RelayCommand(CanExecute = nameof(CanRefreshParameters))]
    private async Task RefreshParametersAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            SetDisconnectedState();
            return;
        }

        CancelLoadOperation();
        loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        var cancellationToken = loadCancellation.Token;
        await SetLoadStateAsync();
        progressDialog = await extendedDialogService.DisplayProgressCancellableAsync(
            "Handling parameters",
            () => ProgressMessage,
            tokenSource: loadCancellation);

        var progress = new Progress<ParameterStreamProgress>(value => dispatcher.Dispatch(() =>
        {
            ShowDataGrid = false;
            ProgressMessage = value.TotalCount > 0
                ? $"Processing parameters... {value.ReceivedCount}/{value.TotalCount}"
                : "Processing parameters...";
        }));

        try
        {
            logger.LogInformation("Loading the Full Parameters List for {VehicleId}.", vehicleId);
            var result = await connectionSession.ParameterStreamService.StreamAllParametersWithRetryAsync(
                vehicleId,
                progress,
                3,
                cancellationToken: cancellationToken);
            if (!result.Success)
            {
                await dispatcher.DispatchAsync(() =>
                {
                    CompleteBusyState();
                    ShowLoadingCompletedWithError = true;
                    StatusMessage = result.ErrorMessage ?? "Parameter loading failed.";
                });
                logger.LogError("Full Parameters List load failed for {VehicleId}: {Error}", vehicleId, result.ErrorMessage);
                return;
            }

            var session = editSessionFactory.Create(vehicleId);
            AttachSession(session);
            await session.LoadAsync(cancellationToken: cancellationToken);
            await dispatcher.DispatchAsync(() =>
            {
                SynchronizeParameterItems();
                CompleteBusyState();
                ShowDataGrid = true;
                StatusMessage = $"Loaded {session.Fields.Count} parameters for {session.Scope.FirmwareIdentity.Family}.";
            });
            logger.LogInformation("Loaded {Count} editable parameter fields for {VehicleId}.", session.Fields.Count, vehicleId);
        }
        catch (OperationCanceledException)
        {
            await dispatcher.DispatchAsync(() =>
            {
                CompleteBusyState();
                ShowLoadingCancelled = true;
                StatusMessage = activeVehicle.IsOnline ? "Parameter loading was cancelled." : "The vehicle disconnected while parameters were loading.";
                ShowVehicleDisconnected = !activeVehicle.IsOnline;
            });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error loading parameters for {VehicleId}.", vehicleId);
            await dispatcher.DispatchAsync(async () =>
            {
                CompleteBusyState();
                ShowLoadingCompletedWithError = true;
                StatusMessage = exception.Message;
                var errorModel = domainFactory.Create<ErrorViewModel, string>(exception.Message + "\nEnsure there is a connection and try again");
                var view = domainFactory.Create<ErrorView, ErrorViewModel>(errorModel);
                await dialogService.DisplayViewAsync("Load failed.", view, "OK");
            });
        }
        finally
        {
            CloseProgressDialog();
            loadCancellation?.Dispose();
            loadCancellation = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelLoad))]
    private void CancelLoad()
    {
        CancelLoadOperation();
        CompleteBusyState();
        ShowLoadingCancelled = true;
        StatusMessage = "Parameter loading was cancelled.";
    }

    [RelayCommand]
    private async Task LoadFromFileAsync()
    {
        if (editSession is null)
        {
            StatusMessage = "Refresh vehicle parameters before importing a parameter file.";
            return;
        }

        try
        {
            var loaded = await parametersFileHandler.LoadParametersFromFileAsync(
                editSession.Fields.Select(ToVehicleParameter).ToList(),
                activeVehicle.ConnectionCancellationToken);
            foreach (var parameter in loaded)
            {
                editSession.TrySetPending(parameter.Name, parameter.Value, out _);
            }

            StatusMessage = $"Imported {loaded.Count} matching values as unapplied edits.";
        }
        catch (Exception exception)
        {
            await dialogService.ConfirmAsync("Load failed", exception.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task LoadFromJsonFileAsync()
    {
        if (editSession is null)
        {
            StatusMessage = "Refresh vehicle parameters before importing a parameter file.";
            return;
        }

        try
        {
            var loaded = await parametersFileHandler.LoadParametersFromJsonFileAsync(activeVehicle.ConnectionCancellationToken);
            foreach (var parameter in loaded)
            {
                editSession.TrySetPending(parameter.Name, parameter.Value, out _);
            }

            StatusMessage = $"Imported {loaded.Count} matching values as unapplied edits.";
        }
        catch (Exception exception)
        {
            await dialogService.ConfirmAsync("Load failed", exception.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task SaveToFileAsync()
    {
        try
        {
            var parameters = editSession?.Fields.Select(ToVehicleParameter).ToList() ?? [];
            var result = await parametersFileHandler.SaveParametersToFile(parameters, CancellationToken.None);
            await dialogService.ConfirmAsync("Saved", $"File saved to:\n{result}", "OK");
        }
        catch (Exception exception)
        {
            await dialogService.ConfirmAsync("Save failed", exception.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task SaveToJsonFileAsync()
    {
        try
        {
            var result = await parametersFileHandler.SaveParametersToJsonFile(allParameterItems, CancellationToken.None);
            await dialogService.ConfirmAsync("Saved", $"File saved to:\n{result}", "OK");
        }
        catch (Exception exception)
        {
            await dialogService.ConfirmAsync("Save failed", exception.Message, "OK");
        }
    }

    [RelayCommand(CanExecute = nameof(CanWriteParameters))]
    private async Task WriteParametersAsync(CancellationToken cancellationToken)
    {
        if (editSession is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = $"Applying {ModifiedParameterCount} modified parameters...";
        try
        {
            using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                activeVehicle.ConnectionCancellationToken);
            var report = await editSession.ApplyAsync(cancellationToken: connectionCancellation.Token);
            RebootRequired |= report.RebootRequired;
            StatusMessage = report.Success
                ? $"Confirmed {report.Confirmed.Count} parameter changes by vehicle readback."
                : $"Confirmed {report.Confirmed.Count}; {report.Failed.Count} changes still require attention.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Parameter apply was cancelled before all values were confirmed.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to apply Full Parameters List edits.");
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CompareParameters() => StatusMessage = "Parameter comparison is not implemented yet.";

    [RelayCommand]
    private void LoadPreSaved() => StatusMessage = "Presaved parameter profiles are not implemented yet.";

    [RelayCommand]
    private void ResetToDefault()
    {
        editSession?.RevertAll();
        StatusMessage = "All unapplied values were reverted to current live values.";
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
        if (editSession is not null)
        {
            editSession.Changed -= OnEditSessionChanged;
        }

        Parameters.Clear();
        allParameterItems.Clear();
    }

    private bool CanRefreshParameters() => HasConnection && !IsBusy;

    private bool CanCancelLoad() => IsBusy;

    private bool CanWriteParameters() =>
        HasConnection && !IsBusy && editSession is { IsDirty: true, IsValid: true };

    private void AttachSession(IParameterEditSession session)
    {
        if (ReferenceEquals(editSession, session))
        {
            return;
        }

        if (editSession is not null)
        {
            editSession.Changed -= OnEditSessionChanged;
        }

        editSession = session;
        editSession.Changed += OnEditSessionChanged;
    }

    private void OnEditSessionChanged(object? sender, EventArgs args)
    {
        dispatcher.Dispatch(() =>
        {
            if (disposed || editSession is null)
            {
                return;
            }

            SynchronizeParameterItems();
            if (!editSession.IsValid)
            {
                StatusMessage = editSession.InvalidReason ?? "This parameter session is stale.";
            }
        });
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        var scopeChanged =
            args.Previous.VehicleId != args.Current.VehicleId ||
            args.Previous.IsOnline != args.Current.IsOnline ||
            args.Previous.State?.Identity.Firmware != args.Current.State?.Identity.Firmware;
        if (!scopeChanged)
        {
            return;
        }

        dispatcher.Dispatch(() =>
        {
            if (!active)
            {
                return;
            }

            HasConnection = args.Current.IsOnline;
            ShowVehicleDisconnected = !HasConnection;
            CancelLoadOperation();
            CompleteBusyState();
            StatusMessage = HasConnection
                ? "Vehicle scope changed. Refresh parameters before editing."
                : editSession?.InvalidReason ?? "The vehicle is disconnected.";
        });
    }

    private void SynchronizeParameterItems()
    {
        if (editSession is null)
        {
            return;
        }

        var itemsByName = allParameterItems.ToDictionary(item => item.Name, StringComparer.Ordinal);
        foreach (var field in editSession.Fields)
        {
            if (itemsByName.TryGetValue(field.Name, out var item))
            {
                item.SetField(field);
            }
            else
            {
                allParameterItems.Add(new ParameterItemViewModel(editSession, field));
            }
        }

        allParameterItems.Sort((left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
        TotalParameterCount = allParameterItems.Count;
        ModifiedParameterCount = editSession.Fields.Count(field => field.IsModified);
        FilterParameters();
        WriteParametersCommand.NotifyCanExecuteChanged();
    }

    private void FilterParameters()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? allParameterItems
            : allParameterItems.Where(item =>
                item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                item.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                item.DisplayName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true).ToList();
        Parameters.Clear();
        Parameters.AddRange(filtered);
        ShowEmptyView = Parameters.Count == 0;
    }

    private async Task SetLoadStateAsync()
    {
        await dispatcher.DispatchAsync(() =>
        {
            CompleteBusyState();
            IsBusy = true;
            ShowLoadingProgress = true;
            ShowDataGrid = false;
            ProgressMessage = "Loading parameters...";
            StatusMessage = ProgressMessage;
        });
    }

    private void CompleteBusyState()
    {
        CloseProgressDialog();
        ProgressMessage = string.Empty;
        IsBusy = false;
        ShowLoadingProgress = false;
        ShowLoadingCompletedWithError = false;
        ShowLoadingCancelled = false;
        ShowEmptyView = Parameters.Count == 0;
    }

    private void SetDisconnectedState()
    {
        HasConnection = false;
        ShowVehicleDisconnected = true;
        StatusMessage = "Connect a vehicle before loading parameters.";
        CompleteBusyState();
    }

    private void CancelLoadOperation()
    {
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = null;
    }

    private void CloseProgressDialog()
    {
        progressDialog?.Dispose();
        progressDialog = null;
    }

    private static VehicleParameter ToVehicleParameter(ParameterEditField field) =>
        new(field.Name, (float)field.PendingValue, field.Type, 0, 0);
}
