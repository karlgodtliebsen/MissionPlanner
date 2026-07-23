using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Common;
using MissionPlanner.Core.ConfigTuning;
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
    private const string DefaultStatusMessage = "Connect a vehicle, then refresh parameters.";
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
    private readonly int pageSize = 100;
    private readonly int? currentPage;

    /// <summary>Gets whether the page is temporarily covered by its owned progress dialog.</summary>
    public bool IsShowingProgressDialog { get; private set; }

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
        Activate();
    }

    /// <summary>Gets the currently visible parameter rows.</summary>
    public ObservableRangeCollection<ParameterItemViewModel> Parameters { get; } = [];

    /// <summary>Gets the current loading-progress message.</summary>
    [ObservableProperty]
    public partial string? ProgressMessage { get; set; } = null;

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
    public partial string? StatusMessage { get; set; }

    /// <summary>Gets the latest error message.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Gets whether at least one confirmed change requires a vehicle reboot.</summary>
    [ObservableProperty]
    public partial bool RebootRequired { get; set; }

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
        StatusMessage = HasConnection ? null : DefaultStatusMessage;
        ErrorMessage = null;
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
        CompleteBusyState();
        StatusMessage = null;
        ErrorMessage = null;
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterParameters();
    }

    private void SetMessages(string? statusMessage = null, string? errorMessage = null)
    {
        StatusMessage = statusMessage;
        ErrorMessage = errorMessage;
    }

    private void CloseOperationDialog()
    {
        if (progressDialog is null)
        {
            IsShowingProgressDialog = false;
            return;
        }

        IsShowingProgressDialog = false;
        progressDialog?.Dispose();
        progressDialog = null;
    }

    [RelayCommand(CanExecute = nameof(CanRefreshParameters))]
    private async Task RefreshParametersAsync()
    {
        SetMessages();
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            SetDisconnectedState();
            return;
        }

        CancelLoadOperation();
        loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        var cancellationToken = loadCancellation.Token;
        CloseOperationDialog();
        ProgressMessage = string.Empty;
        try
        {
            await SetLoadStateAsync();
            IsShowingProgressDialog = true;
            progressDialog = await extendedDialogService.DisplayProgressCancellableAsync("Handling parameters", () => ProgressMessage, tokenSource: loadCancellation);
            var progress = CreateProgress();
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("Loading the Full Parameters List for {VehicleId}.", vehicleId);

            var result = await connectionSession.ParameterStreamService.StreamAllParametersWithRetryAsync(vehicleId, progress, 3, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!result.Success)
            {
                CloseOperationDialog();
                await dispatcher.DispatchAsync(() =>
                {
                    CompleteBusyState();
                    ShowLoadingCompletedWithError = true;
                    SetMessages(errorMessage: result.ErrorMessage ?? "Parameter loading failed.");
                });
                logger.LogError("Full Parameters List load failed for {VehicleId}: {Error}", vehicleId, result.ErrorMessage);
                return;
            }

            progress?.Report(new ParameterStreamProgress(Message: $"Loaded {result.Parameters.Count} parameters."));

            var session = editSessionFactory.Create(vehicleId);

            progress?.Report(new ParameterStreamProgress(Message: $"Loading Metadata for {result.Parameters.Count} parameters.."));

            await session.LoadAsync(cancellationToken: cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            AttachSession(session);
            CloseOperationDialog();
            await dispatcher.DispatchAsync(() =>
            {
                SynchronizeParameterItems(progress);
                CompleteBusyState();
                SetMessages($"Loaded {session.Fields.Count} parameters for {session.Scope.FirmwareIdentity.Family}.");
            });
            logger.LogInformation("Loaded {Count} editable parameter fields for {VehicleId}.", session.Fields.Count, vehicleId);
        }
        catch (OperationCanceledException)
        {
            await dispatcher.DispatchAsync(() =>
            {
                CompleteBusyState();
                ShowLoadingCancelled = true;
                SetMessages(errorMessage: activeVehicle.IsOnline ? "Parameter loading was cancelled." : "The vehicle disconnected while parameters were loading.");
                ShowVehicleDisconnected = !activeVehicle.IsOnline;
            });
        }
        catch (Exception exception)
        {
            CloseOperationDialog();
            logger.LogError(exception, "Error loading parameters for {VehicleId}.", vehicleId);
            await dispatcher.DispatchAsync(async () =>
            {
                CompleteBusyState();
                ShowLoadingCompletedWithError = true;
                var m = exception.Message;
                SetMessages(null, m);
                var errorModel = domainFactory.Create<ErrorViewModel, string>(exception.Message + "\nEnsure there is a connection and try again");
                var view = domainFactory.Create<ErrorView, ErrorViewModel>(errorModel);
                await dialogService.DisplayViewAsync("Load failed.", view, "OK");
            });
        }
        finally
        {
            CloseOperationDialog();
            loadCancellation?.Dispose();
            loadCancellation = null;
        }
    }

    private IProgress<ParameterStreamProgress> CreateProgress()
    {
        var progress = new Progress<ParameterStreamProgress>(value => dispatcher.Dispatch(() => ProgressMessage = value.Message ?? (value.TotalCount > 0
            ? $"Processing parameters... {value.ReceivedCount}/{value.TotalCount}"
            : "Processing parameters...")));
        return progress;
    }

    [RelayCommand(CanExecute = nameof(CanCancelLoad))]
    private void CancelLoad()
    {
        CancelLoadOperation();
        CloseProgressDialog();
        CompleteBusyState();
        ShowLoadingCancelled = true;
        SetMessages(errorMessage: "Parameter loading was cancelled.");
    }

    [RelayCommand]
    private async Task LoadFromFileAsync()
    {
        if (editSession is null)
        {
            SetMessages(errorMessage: "Refresh vehicle parameters before importing a parameter file.");
            return;
        }

        try
        {
            var loaded = await parametersFileHandler.LoadParametersFromFileAsync(
                editSession.Fields.Select(ToVehicleParameter).ToList(),
                activeVehicle.ConnectionCancellationToken);
            foreach (var parameter in loaded)
            {
                editSession.TrySetPending(parameter.Name, parameter.Value, out var _);
            }

            SetMessages($"Imported {loaded.Count} matching values as unapplied edits.");
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
            SetMessages(errorMessage: "Refresh vehicle parameters before importing a parameter file.");
            return;
        }

        try
        {
            var loaded = await parametersFileHandler.LoadParametersFromJsonFileAsync(activeVehicle.ConnectionCancellationToken);
            foreach (var parameter in loaded)
            {
                editSession.TrySetPending(parameter.Name, parameter.Value, out var _);
            }

            SetMessages($"Imported {loaded.Count} matching values as unapplied edits.");
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
        var m = $"Applying {ModifiedParameterCount} modified parameters...";
        SetMessages(m);

        try
        {
            using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                activeVehicle.ConnectionCancellationToken);
            var report = await editSession.ApplyAsync(cancellationToken: connectionCancellation.Token);
            RebootRequired |= report.RebootRequired;
            var statusMessage = report.Success
                ? $"Confirmed {report.Confirmed.Count} parameter changes by vehicle readback."
                : null;
            var errorMessage = report.Success
                ? null
                : report.Failed.Count > 0
                    ? $"Failed to apply {report.Failed.Count} parameters."
                    : "Some parameters were not confirmed by the vehicle.";

            SetMessages(statusMessage, errorMessage);
        }
        catch (OperationCanceledException)
        {
            m = "Parameter apply was cancelled before all values were confirmed.";
            SetMessages(null, m);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to apply Full Parameters List edits.");
            m = exception.Message;
            SetMessages(null, m);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CompareParameters()
    {
        var m = "Parameter comparison is not implemented yet.";
        SetMessages(m);
    }

    [RelayCommand]
    private void LoadPreSaved()
    {
        var m = "Presaved parameter profiles are not implemented yet.";
        SetMessages(m);
    }

    [RelayCommand]
    private void ResetToDefault()
    {
        editSession?.RevertAll();
        var m = "All unapplied values were reverted to current live values.";
        SetMessages(m);
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
        editSession?.Changed -= OnEditSessionChanged;

        Parameters.Clear();
        allParameterItems.Clear();
    }

    private bool CanRefreshParameters()
    {
        return HasConnection && !IsBusy;
    }

    private bool CanCancelLoad()
    {
        return IsBusy;
    }

    private bool CanWriteParameters()
    {
        return HasConnection && !IsBusy && editSession is { IsDirty: true, IsValid: true };
    }

    private void AttachSession(IParameterEditSession session)
    {
        if (ReferenceEquals(editSession, session))
        {
            return;
        }

        editSession?.Changed -= OnEditSessionChanged;

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
                var m = editSession.InvalidReason ?? "This parameter session is stale.";
                SetMessages(m);
            }
        });
    }

    private void SynchronizeParameterItems(IProgress<ParameterStreamProgress>? progress = null)
    {
        if (editSession is null)
        {
            return;
        }

        progress?.Report(new ParameterStreamProgress(Message: $"Creating data grid for {editSession.Fields.Count} parameters"));
        Task.Delay(1000);

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
        Parameters.AddRange(filtered.Skip((pageSize * currentPage) ?? 0).Take(pageSize));
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
            CloseProgressDialog();
            CompleteBusyState();
            var statusMessage = HasConnection ? "Vehicle scope changed. Refresh parameters before editing." : null;
            var errorMessage = !HasConnection ? null : editSession?.InvalidReason ?? "The vehicle is disconnected.";
            SetMessages(statusMessage, errorMessage);
        });
    }

    private async Task SetLoadStateAsync()
    {
        await dispatcher.DispatchAsync(() =>
        {
            CloseProgressDialog();
            CompleteBusyState();
            IsBusy = true;
            ShowLoadingProgress = true;
            ProgressMessage = "Loading parameters...";
            SetMessages(ProgressMessage);
        });
    }

    private void CompleteBusyState()
    {
        ProgressMessage = string.Empty;
        IsBusy = false;
        ShowLoadingProgress = false;
        ShowLoadingCompletedWithError = false;
        ShowLoadingCancelled = false;
    }

    private void SetDisconnectedState()
    {
        HasConnection = false;
        ShowVehicleDisconnected = true;
        var m = DefaultStatusMessage;
        SetMessages(null, m);
        CloseProgressDialog();
        CompleteBusyState();
    }

    private void CancelLoadOperation()
    {
        var cancellation = loadCancellation;
        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The owning load completed between observing and cancelling the source.
        }
    }

    private void CloseProgressDialog()
    {
        IsShowingProgressDialog = false;
        progressDialog?.Dispose();
        progressDialog = null;
    }

    private static VehicleParameter ToVehicleParameter(ParameterEditField field)
    {
        return new VehicleParameter(field.Name, (float)field.PendingValue, field.Type, 0, 0);
    }
}
