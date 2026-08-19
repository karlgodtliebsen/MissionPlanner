using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using UraniumUI.Material.Dialogs;

namespace MissionPlanner.App.Views.Common;

/// <summary>Provides the searchable full parameter list through the shared safe editing session.</summary>
public partial class ParametersViewModel : ObservableObject, IDisposable
{
    private const string DefaultStatusMessage = "Connect a vehicle, then refresh parameters.";
    private readonly IVehicleConnectionSession connectionSession;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IVehicleParameterLoadStatusContext parameterLoadStatus;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IParameterEditSessionFactory editSessionFactory;
    private readonly IDispatcher dispatcher;
    private readonly IExtendedDialogService dialogService;
    private readonly IDomainFactory domainFactory;
    private readonly ILogger logger;
    private readonly IDisposable parameterLoadStatusSubscription;
    private CancellationTokenSource? loadCancellation;
    private CancellationTokenSource? cachedLoadCancellation;

    /// <summary>
    /// The shared parameter editing session.
    /// </summary>
    protected IParameterEditSession? EditSession;

    private ParameterApplyReport? lastApplyReport;
    private IDisposable? progressDialog;
    private bool disposed;
    private int sessionRefreshScheduled;
    private int cachedLoadScheduled;

    /// <summary>Gets whether the page is temporarily covered by its owned progress dialog.</summary>
    public bool IsShowingProgressDialog { get; private set; }

    /// <summary>Initializes the Full Parameters List tab.</summary>
    /// <param name="connectionSession">The current connection-scoped services.</param>
    /// <param name="activeVehicle">The application active-vehicle context.</param>
    /// <param name="editSessionFactory">The shared parameter editing-session factory.</param>
    /// <param name="dispatcher">The UI Dispatcher.</param>
    /// <param name="dialogService">The extended dialog service.</param>
    /// <param name="domainFactory">The domain view factory.</param>
    /// <param name="parameterLoadStatus"></param>
    /// <param name="domainEventHub"></param>
    /// <param name="logger">The logger.</param>
    protected ParametersViewModel(
        IVehicleConnectionSession connectionSession,
        IActiveVehicleContext activeVehicle,
        IParameterEditSessionFactory editSessionFactory,
        IDispatcher dispatcher,
        IExtendedDialogService dialogService,
        IDomainFactory domainFactory,
        IVehicleParameterLoadStatusContext parameterLoadStatus,
        IDomainEventHub domainEventHub,
        ILogger logger)
    {
        this.connectionSession = connectionSession;
        parameterRegistry = connectionSession.ParameterRegistry;
        this.activeVehicle = activeVehicle;
        this.editSessionFactory = editSessionFactory;
        this.dispatcher = dispatcher;
        this.dialogService = dialogService;
        this.domainFactory = domainFactory;
        this.parameterLoadStatus = parameterLoadStatus;
        this.logger = logger;
        parameterLoadStatusSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleParameterLoadStatusChanged>(OnParameterLoadStatusChanged);
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

    /// <summary>
    /// Gets whether the active vehicle is disconnected.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowVehicleDisconnected { get; set; }

    /// <summary>
    /// Gets the number of unapplied parameter values.
    /// </summary>
    [ObservableProperty]
    public partial int ModifiedParameterCount { get; set; }

    /// <summary>
    /// Gets the total number of loaded parameter fields.
    /// </summary>
    [ObservableProperty]
    public partial int TotalParameterCount { get; set; }

    /// <summary>
    /// Gets whether a load or apply operation is active.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Gets whether the connection-owned background parameter download is active.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    public partial bool IsBackgroundParameterLoadInProgress { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    public partial bool HasParameters { get; set; }

    /// <summary>Gets whether an active vehicle connection is available.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    public partial bool HasConnection { get; set; }

    /// <summary>Gets the latest editing or apply status.</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>Gets the latest error message.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the current operation progress from zero to one.</summary>
    [ObservableProperty]
    public partial double Progress { get; set; }

    /// <summary>Activates vehicle lifecycle tracking while the tab is visible.</summary>
    protected void InitializeParameters()
    {
        if (disposed)
        {
            return;
        }

        ErrorMessage = null;
        IsBusy = false;
        HasParameters = Parameters.Count > 0;
        activeVehicle.Changed += OnActiveVehicleChanged;
        parameterRegistry.Changed += OnParameterRegistryChanged;
        HasConnection = activeVehicle.IsOnline;
        ShowVehicleDisconnected = !HasConnection;
        StatusMessage = HasConnection ? null : DefaultStatusMessage;

        if (activeVehicle.VehicleId is { } vehicleId && HasConnection)
        {
            ApplyParameterLoadStatus(parameterLoadStatus.Get(vehicleId));
            ScheduleCachedParameterLoad(vehicleId);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="vehicleChangedEventArgs"></param>
    protected virtual void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs vehicleChangedEventArgs)
    {
        var scopeChanged =
            vehicleChangedEventArgs.Previous.VehicleId != vehicleChangedEventArgs.Current.VehicleId ||
            vehicleChangedEventArgs.Previous.IsOnline != vehicleChangedEventArgs.Current.IsOnline ||
            vehicleChangedEventArgs.Previous.State?.Identity.Firmware != vehicleChangedEventArgs.Current.State?.Identity.Firmware;
        if (!scopeChanged)
        {
            return;
        }

        var changed = vehicleChangedEventArgs.Current.IsOnline;

        dispatcher.Dispatch(() =>
        {
            editSessionFactory?.DiscardPendingChanges();
            CancelCachedParameterLoad();
            EditSession?.Changed -= OnEditSessionChanged;
            EditSession = null;
            Parameters.Clear();
            HasParameters = false;
            TotalParameterCount = 0;
            HasConnection = changed;
            ShowVehicleDisconnected = !changed;
            CancelLoadOperation();
            CloseProgressDialog();
            CompleteBusyState();

            var statusMessage = changed ? "Vehicle changed. Refresh parameters." : null;
            var errorMessage = changed ? null : "The vehicle is disconnected.";
            Debug.Assert(statusMessage is null || errorMessage is null);
            SetMessages(statusMessage, errorMessage);

            if (changed && vehicleChangedEventArgs.Current.VehicleId is { } vehicleId)
            {
                ApplyParameterLoadStatus(parameterLoadStatus.Get(vehicleId));
                ScheduleCachedParameterLoad(vehicleId);
            }
        });
    }

    /// <summary>
    ///  
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    protected virtual void OnEditSessionChanged(object? sender, EventArgs e)
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="evt"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected virtual Task OnParameterLoadStatusChanged(VehicleParameterLoadStatusChanged evt, CancellationToken cancellationToken)
    {
        var status = evt.Status;
        if (disposed || activeVehicle.VehicleId != status.VehicleId)
        {
            return Task.CompletedTask;
        }

        dispatcher.Dispatch(() =>
        {
            var latest = parameterLoadStatus.Get(status.VehicleId);
            if (latest != status)
            {
                return;
            }

            ApplyParameterLoadStatus(latest);
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="status"></param>
    protected virtual void ApplyParameterLoadStatus(ParameterLoadStatus? status)
    {
        if (status is null || activeVehicle.VehicleId != status.VehicleId)
        {
            return;
        }

        IsBackgroundParameterLoadInProgress = status.IsInProgress;
        ShowLoadingProgress = status.IsInProgress;
        ProgressMessage = status.Message;

        switch (status.State)
        {
            case ParameterLoadState.Starting:
            case ParameterLoadState.Downloading:
                ShowLoadingCompletedWithError = false;
                ShowLoadingCancelled = false;
                SetMessages(status.Message);
                break;
            case ParameterLoadState.Completed:
                ShowLoadingCompletedWithError = false;
                ShowLoadingCancelled = false;
                SetMessages(status.Message);
                ScheduleCachedParameterLoad(status.VehicleId);
                break;
            case ParameterLoadState.Failed:
                ShowLoadingCompletedWithError = true;
                SetMessages(errorMessage: status.Message);
                break;
            case ParameterLoadState.Cancelled:
                ShowLoadingCancelled = true;
                SetMessages(errorMessage: status.Message);
                break;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    protected virtual void OnParameterRegistryChanged(object? sender, VehicleParameterChangedEventArgs args)
    {
        if (disposed ||
            !activeVehicle.IsOnline ||
            activeVehicle.VehicleId != args.VehicleId ||
            args.Parameter is null)
        {
            return;
        }

        ScheduleCachedParameterLoad(args.VehicleId);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vehicleId"></param>
    protected virtual void ScheduleCachedParameterLoad(VehicleId vehicleId)
    {
        if (disposed || IsBusy || !HasCompleteCachedParameterSet(vehicleId) ||
            Interlocked.CompareExchange(ref cachedLoadScheduled, 1, 0) != 0)
        {
            return;
        }

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            activeVehicle.ConnectionCancellationToken);
        cachedLoadCancellation = cancellation;
        _ = LoadCachedParametersAsync(vehicleId, cancellation);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vehicleId"></param>
    /// <returns></returns>
    protected virtual bool HasCompleteCachedParameterSet(VehicleId vehicleId)
    {
        var expectedCount = parameterRegistry.GetParameterCount(vehicleId);
        return expectedCount is > 0 &&
               parameterRegistry.GetAllParameters(vehicleId).Count >= expectedCount.Value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vehicleId"></param>
    /// <param name="cancellation"></param>
    protected virtual async Task LoadCachedParametersAsync(VehicleId vehicleId, CancellationTokenSource cancellation)
    {
        try
        {
            var session = editSessionFactory.Create(vehicleId);
            await session.LoadAsync(cancellationToken: cancellation.Token).ConfigureAwait(false);
            cancellation.Token.ThrowIfCancellationRequested();

            await dispatcher.DispatchAsync(() =>
            {
                if (disposed ||
                    !activeVehicle.IsOnline ||
                    activeVehicle.VehicleId != vehicleId)
                {
                    return;
                }

                AttachSession(session);
                CompleteBusyState();
                SetMessages($"Loaded {session.Fields.Count} cached parameters.");
            });
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The active vehicle changed or the retained page was released.
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not project cached parameters for {VehicleId}.",
                vehicleId);
        }
        finally
        {
            Interlocked.CompareExchange(ref cachedLoadCancellation, null, cancellation);
            cancellation.Dispose();
            Interlocked.Exchange(ref cachedLoadScheduled, 0);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    protected virtual void CancelCachedParameterLoad()
    {
        var cancellation = Interlocked.Exchange(ref cachedLoadCancellation, null);
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
            // Completion won the race with cancellation.
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="statusMessage"></param>
    /// <param name="errorMessage"></param>
    protected virtual void SetMessages(string? statusMessage = null, string? errorMessage = null)
    {
        StatusMessage = statusMessage;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="result"></param>
    /// <param name="vehicleId"></param>
    protected virtual async Task HandleLoadError(ParameterStreamResult result, VehicleId vehicleId)
    {
        await dispatcher.DispatchAsync(() =>
        {
            CompleteBusyState();
            ShowLoadingCompletedWithError = true;
            SetMessages(errorMessage: result.ErrorMessage ?? "Parameter loading failed.");
        });
        logger.LogError("Full Parameters List load failed for {VehicleId}: {Error}", vehicleId, result.ErrorMessage);
        HasParameters = Parameters.Count > 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    protected virtual IProgress<ParameterStreamProgress> CreateProgress()
    {
        var progress = new Progress<ParameterStreamProgress>(value => dispatcher.Dispatch(() => ProgressMessage = value.Message ?? (value.TotalCount > 0
            ? $"Processing parameters... {value.ReceivedCount}/{value.TotalCount}"
            : "Processing parameters...")));
        return progress;
    }


    /// <summary>
    /// 
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRefreshParameters))]
    protected virtual async Task RefreshParametersAsync()
    {
        SetMessages();
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline || IsBackgroundParameterLoadInProgress)
        {
            return;
        }

        CloseProgressDialog();
        CancelCachedParameterLoad();
        CancelLoadOperation();
        loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        var cancellationToken = loadCancellation.Token;
        ProgressMessage = string.Empty;
        try
        {
            await SetLoadStateAsync();
            IsShowingProgressDialog = true;
            progressDialog = await dialogService.DisplayProgressCancellableAsync("Loading parameters", () => ProgressMessage, tokenSource: loadCancellation);
            var progress = CreateProgress();
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("Loading the Full Parameters List for {VehicleId}.", vehicleId);

            var result = await connectionSession.ParameterStreamService.StreamAllParametersWithRetryAsync(vehicleId, progress, 3, cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!result.Success)
            {
                await HandleLoadError(result, vehicleId);
                return;
            }

            progress?.Report(new ParameterStreamProgress(Message: $"Loaded {result.Parameters.Count} parameters."));

            var session = editSessionFactory.Create(vehicleId);

            progress?.Report(new ParameterStreamProgress(Message: $"Loading Metadata for {result.Parameters.Count} parameters.."));

            await session.LoadAsync(cancellationToken: cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            await dispatcher.DispatchAsync(() =>
            {
                if (disposed ||
                    !activeVehicle.IsOnline ||
                    activeVehicle.VehicleId != vehicleId)
                {
                    return;
                }

                AttachSession(session);
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
            logger.LogError(exception, "Error loading parameters for {VehicleId}.", vehicleId);
            await dispatcher.DispatchAsync(async () =>
            {
                CompleteBusyState();
                ShowLoadingCompletedWithError = true;
                var m = exception.Message;
                SetMessages(null, m);
                var errorModel = domainFactory.Create<ErrorViewModel, string>(exception.Message + "\nEnsure there is a connection and try again");
                var view = domainFactory.Create<ErrorView, ErrorViewModel>(errorModel);
                await dialogService.DisplayViewExtendedAsync("Load failed.", view, "OK");
            });
        }
        finally
        {
            loadCancellation?.Dispose();
            loadCancellation = null;
            HasParameters = Parameters.Count > 0;
            CloseProgressDialog();
        }
    }


    [RelayCommand(CanExecute = nameof(CanCancelLoad))]
    protected virtual void CancelLoad()
    {
        SetMessages();
        var isBusy = IsBusy;
        CancelLoadOperation();
        CloseProgressDialog();
        CompleteBusyState();
        if (isBusy)
        {
            ShowLoadingCancelled = true;
            SetMessages(errorMessage: "Parameter loading was cancelled.");
        }
    }

    /// <summary>
    /// Determines whether the view model can refresh parameters.
    /// </summary>
    /// <returns><c>true</c> if the view model can refresh parameters; otherwise, <c>false</c>.</returns>
    protected virtual bool CanRefreshParameters()
    {
        return HasConnection && !IsBusy && !IsBackgroundParameterLoadInProgress;
    }

    /// <summary>
    /// Determines whether the view model can cancel the current parameter load operation.
    /// </summary>
    /// <returns><c>true</c> if the view model can cancel the load operation; otherwise, <c>false</c>.</returns>
    protected virtual bool CanCancelLoad()
    {
        return IsBusy;
    }


    /// <summary>
    /// Determines whether the view model can retry failed parameter operations.
    /// </summary>
    /// <returns><c>true</c> if the view model can retry failed operations; otherwise, <c>false</c>.</returns>
    protected virtual bool CanRetryFailed()
    {
        return HasConnection && !IsBusy && EditSession is { IsValid: true } && lastApplyReport?.Retryable.Count > 0;
    }


    /// <summary>
    /// Attaches the given parameter edit session to the view model.
    /// </summary>
    /// <param name="session">The parameter edit session to attach.</param>
    protected virtual void AttachSession(IParameterEditSession session)
    {
        if (ReferenceEquals(EditSession, session))
        {
            return;
        }

        EditSession?.Changed -= OnEditSessionChanged;
        EditSession = session;
        EditSession.Changed += OnEditSessionChanged;

        // Loading a session does not raise Changed. Notify the derived view model
        // explicitly so it can create its initial UI projection.
        OnEditSessionChanged(EditSession, EventArgs.Empty);
    }

    /// <summary>
    /// Sets the load state, updating the UI to reflect that a parameter load operation is in progress.
    /// </summary>
    protected virtual async Task SetLoadStateAsync()
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

    /// <summary>
    /// Completes the busy state, resetting all related flags and messages.
    /// </summary>
    protected virtual void CompleteBusyState()
    {
        ProgressMessage = string.Empty;
        IsBusy = false;
        ShowLoadingProgress = false;
        ShowLoadingCompletedWithError = false;
        ShowLoadingCancelled = false;
    }

    /// <summary>
    /// Cancels the current parameter load operation if it is in progress.
    /// </summary>
    protected virtual void CancelLoadOperation()
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

    /// <inheritdoc />
    public virtual void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Interlocked.Exchange(ref sessionRefreshScheduled, 0);
        activeVehicle.Changed -= OnActiveVehicleChanged;
        parameterRegistry.Changed -= OnParameterRegistryChanged;
        parameterLoadStatusSubscription.Dispose();
        CancelCachedParameterLoad();
        CancelLoadOperation();
        CloseProgressDialog();
        EditSession?.Changed -= OnEditSessionChanged;
        EditSession = null;

        // The page is retained by Shell even though this view model is transient.
        // Release the large row graph immediately so recycled editor controls and
        // parameter metadata do not remain rooted while another page is active.
        Parameters.Clear();
        lastApplyReport = null;
        HasParameters = false;
        TotalParameterCount = 0;
        ModifiedParameterCount = 0;
    }

    /// <summary>
    /// Closes the progress dialog if it is currently shown.
    /// </summary>
    protected virtual void CloseProgressDialog()
    {
        IsShowingProgressDialog = false;
        progressDialog?.Dispose();
        progressDialog = null;
    }
}
