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
using UraniumUI.Extensions;
using UraniumUI.Material.Dialogs;

namespace MissionPlanner.App.Views.Common;

/// <summary>
/// Provides the searchable full parameter list through the shared safe editing session.
/// </summary>
public partial class ParametersViewModel : VehicleConnectionViewModel
{
    private const string DefaultStatusMessage = "Connect a vehicle, then refresh parameters.";
    private readonly IVehicleConnectionSession connectionSession;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IVehicleParameterLoadStatusContext parameterLoadStatus;
    private readonly IDomainEventHub domainEventHub;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IParameterEditSessionFactory editSessionFactory;
    private readonly IDispatcher dispatcher;
    private readonly IExtendedDialogService dialogService;
    private readonly IDomainFactory domainFactory;
    private readonly ILogger logger;

    private bool disposed;
    private bool activated;
    private IDisposable parameterLoadStatusSubscription = null!;
    private CancellationTokenSource? loadCancellation;
    private CancellationTokenSource? cachedLoadCancellation;

    /// <summary>
    /// The shared parameter editing session.
    /// </summary>
    protected IParameterEditSession? EditSession;

    private ParameterApplyReport? lastApplyReport;
    private IDisposable? progressDialog;

    private int sessionRefreshScheduled;
    private int cachedLoadScheduled;

    /// <summary>Gets whether the page is temporarily covered by its owned progress dialog.</summary>
    public bool IsShowingProgressDialog
    {
        get; private set;
    }

    /// <summary>Initializes the Full Parameters List tab.</summary>
    /// <param name="connectionSession">The current connection-scoped services.</param>
    /// <param name="activeVehicle">The application active-vehicle context.</param>
    /// <param name="editSessionFactory">The shared parameter editing-session factory.</param>
    /// <param name="dispatcher">The UI Dispatcher.</param>
    /// <param name="dialogService">The extended dialog service.</param>
    /// <param name="domainFactory">The domain factory.</param>
    /// <param name="parameterLoadStatus">The vehicle parameter load status context.</param>
    /// <param name="domainEventHub">The domain event hub.</param>
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
        ILogger logger) : base(connectionSession, activeVehicle, dispatcher, logger)
    {
        this.connectionSession = connectionSession;
        this.parameterRegistry = connectionSession.ParameterRegistry;
        this.activeVehicle = activeVehicle;
        this.editSessionFactory = editSessionFactory;
        this.dispatcher = dispatcher;
        this.dialogService = dialogService;
        this.domainFactory = domainFactory;
        this.parameterLoadStatus = parameterLoadStatus;
        this.domainEventHub = domainEventHub;
        this.logger = logger;
    }

    /// <summary>
    /// Gets the currently visible parameter rows.
    /// </summary>
    public ObservableRangeCollection<ParameterItemViewModel> Parameters { get; } = [];

    /// <summary>
    /// Gets the current loading-progress message.
    /// </summary>
    [ObservableProperty]
    public partial string? ProgressMessage { get; set; } = null;

    /// <summary>
    /// Gets whether parameter loading is in progress.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowLoadingProgress
    {
        get; set;
    }

    /// <summary>Gets whether the most recent load failed.</summary>
    [ObservableProperty]
    public partial bool ShowLoadingCompletedWithError
    {
        get; set;
    }

    /// <summary>Gets whether the most recent load was cancelled.</summary>
    [ObservableProperty]
    public partial bool ShowLoadingCancelled
    {
        get; set;
    }


    /// <summary>
    /// Gets the number of unapplied parameter values.
    /// </summary>
    [ObservableProperty]
    public partial int ModifiedParameterCount
    {
        get; set;
    }

    /// <summary>
    /// Gets the total number of loaded parameter fields.
    /// </summary>
    [ObservableProperty]
    public partial int TotalParameterCount
    {
        get; set;
    }


    /// <summary>
    /// Gets whether a load or apply operation is active.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    public override partial bool IsBusy
    {
        get; set;
    }

    /// <summary>
    /// Gets whether the connection-owned background parameter download is active.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    public partial bool IsBackgroundParameterLoadInProgress
    {
        get; set;
    }


    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    public partial bool HasParameters
    {
        get; set;
    }

    /// <summary>Gets whether an active vehicle connection is available.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    public override partial bool HasConnection
    {
        get; set;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vehicleChangedEventArgs"></param>
    protected override async Task OnActiveVehicleChanged(ActiveVehicleChangedEventArgs vehicleChangedEventArgs)
    {
        await base.OnActiveVehicleChanged(vehicleChangedEventArgs);


        var changed = vehicleChangedEventArgs.Current.IsOnline;

        await dispatcher.DispatchAsync(async () =>
        {
            SetMessages("Vehicle changed. Refresh parameters.", "The vehicle is disconnected.");
            editSessionFactory?.DiscardPendingChanges();
            CancelCachedParameterLoad();
            EditSession?.Changed -= OnEditSessionChanged;
            EditSession = null;
            Parameters.Clear();
            HasParameters = false;
            TotalParameterCount = 0;
            CancelLoadOperation();
            CloseProgressDialog();
            CompleteBusyState();
            if (changed && vehicleChangedEventArgs.Current.VehicleId is { } vehicleId)
            {
                await ApplyParameterLoadStatusAsync(parameterLoadStatus.Get(vehicleId));
                await ScheduleCachedParameterLoadAsync(vehicleId);
            }
        });
    }

    /// <summary>
    ///  
    /// </summary>
    protected virtual void OnEditSessionChanged()
    {
        if (disposed || !activated || EditSession is null ||
            Interlocked.CompareExchange(ref sessionRefreshScheduled, 1, 0) != 0)
        {
            return;
        }

        void SynchronizeOnUiThread()
        {
            try
            {
                if (disposed || !activated || EditSession is null)
                {
                    return;
                }

                Debug.Print("OnEditSessionChanged-Edit session changed for {0}.", EditSession.VehicleId);
                SynchronizeParameterItems(CreateProgress());
                if (!EditSession.IsValid)
                {
                    SetMessages(EditSession.InvalidReason ?? "This parameter session is stale.");
                }
            }
            finally
            {
                Interlocked.Exchange(ref sessionRefreshScheduled, 0);
            }
        }

        if (!dispatcher.IsDispatchRequired)
        {
            SynchronizeOnUiThread();
            return;
        }

        try
        {
            if (!dispatcher.Dispatch(SynchronizeOnUiThread))
            {
                Interlocked.Exchange(ref sessionRefreshScheduled, 0);
                logger.LogWarning("Could not dispatch the parameter-grid synchronization to the UI thread.");
            }
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(ref sessionRefreshScheduled, 0);
            logger.LogWarning(exception, "Could not dispatch parameter-grid synchronization to the UI thread.");
        }
    }

    private void SynchronizeParameterItems(IProgress<ParameterStreamProgress>? progress = null)
    {
        Debug.Print("SynchronizeParameterItems-Synchronizing parameter items for {0}.", EditSession?.VehicleId);
        if (EditSession is null)
        {
            return;
        }

        var fields = EditSession.Fields;
        progress?.Report(new ParameterStreamProgress(Message: $"Creating data grid for {fields.Count} parameters"));

        var itemsByName = Parameters.ToDictionary(item => item.Name, StringComparer.Ordinal);
        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        var structureChanged = false;
        var nextItems = new List<ParameterItemViewModel>(fields.Count);
        foreach (var field in fields)
        {
            Debug.Print("SynchronizeParameterItems-Field {0}.", field.Name);

            fieldNames.Add(field.Name);
            if (itemsByName.TryGetValue(field.Name, out var item))
            {
                item.SetField(field);
                nextItems.Add(item);
            }
            else
            {
                nextItems.Add(new ParameterItemViewModel(EditSession, field));
                structureChanged = true;
            }
        }

        if (Parameters.Any(item => !fieldNames.Contains(item.Name)))
        {
            structureChanged = true;
        }

        dispatcher.Dispatch(() =>
        {
            if (structureChanged)
            {
                nextItems.Sort((left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
                Debug.Print("SynchronizeParameterItems-Parameters.ReplaceRange {0}.", nextItems.Count);
                Parameters.ReplaceRange(nextItems);
            }
            TotalParameterCount = Parameters.Count;
            ModifiedParameterCount = fields.Count(field => field.IsModified);
        });
        progress?.Report(new ParameterStreamProgress(Message: $"Completed data grid for {fields.Count} parameters"));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="evt"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    protected virtual async Task OnParameterLoadStatusChanged(VehicleParameterLoadStatusChanged evt, CancellationToken cancellationToken)
    {
        var status = evt.Status;
        if (disposed || activeVehicle.VehicleId != status.VehicleId)
        {
            return;
        }

        await dispatcher.DispatchAsync(async () =>
        {
            var latest = parameterLoadStatus.Get(status.VehicleId);
            if (latest != status)
            {
                return;
            }

            await ApplyParameterLoadStatusAsync(latest);
        });
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="status"></param>
    protected virtual async Task ApplyParameterLoadStatusAsync(ParameterLoadStatus? status)
    {
        if (status is null || activeVehicle.VehicleId != status.VehicleId)
        {
            return;
        }
        Debug.Print("ApplyParameterLoadStatusAsync-Applying parameter load status {0} for {1}.", status.State, status.VehicleId);
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
                await ScheduleCachedParameterLoadAsync(status.VehicleId);
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
    /// <param name="args"></param>
    protected virtual async Task OnParameterRegistryChangedAsync(VehicleParameterChangedEventArgs args)
    {
        if (disposed || !activated || EditSession is not null || !activeVehicle.IsOnline ||
            activeVehicle.VehicleId != args.VehicleId || args.Parameter is null)
        {
            return;
        }

        Debug.Print("OnParameterRegistryChangedAsync-Parameter registry changed for {0}.", args.VehicleId);
        await ScheduleCachedParameterLoadAsync(args.VehicleId);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vehicleId"></param>
    protected virtual async Task ScheduleCachedParameterLoadAsync(VehicleId vehicleId)
    {
        Debug.Print("ScheduleCachedParameterLoadAsync-Scheduling cached parameter load for {0}.", vehicleId);
        if (disposed || IsBusy || !HasCompleteCachedParameterSet(vehicleId) || Interlocked.CompareExchange(ref cachedLoadScheduled, 1, 0) != 0)
        {
            return;
        }

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        cachedLoadCancellation = cancellation;
        await LoadCachedParametersAsync(vehicleId, cancellation);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="vehicleId"></param>
    /// <returns></returns>
    protected virtual bool HasCompleteCachedParameterSet(VehicleId vehicleId)
    {
        var expectedCount = parameterRegistry.GetParameterCount(vehicleId);
        var hasCompleteSet = expectedCount is > 0 && parameterRegistry.GetAllParameters(vehicleId).Count >= expectedCount.Value;
        Debug.Print("HasCompleteCachedParameterSet-Has complete cached parameter set for {0}: {1}.", vehicleId, hasCompleteSet);
        return hasCompleteSet;
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
            Debug.Print("LoadCachedParametersAsync-Loading cached parameters for {0}.", vehicleId);
            var session = editSessionFactory.Create(vehicleId);
            await session.LoadAsync(cancellationToken: cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();

            await dispatcher.DispatchAsync(() =>
            {
                if (disposed || !activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId)
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
            logger.LogWarning(exception, "Could not project cached parameters for {VehicleId}.", vehicleId);
            Debug.WriteLine(exception);
            await dispatcher.DispatchAsync(() =>
            {
                if (!disposed && activated && activeVehicle.VehicleId == vehicleId)
                {
                    CompleteBusyState();
                    SetMessages(errorMessage: $"Could not display cached parameters: {exception.Message}");
                }
            });
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
    protected virtual async Task ClearParametersAsync()
    {
        SetMessages();
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline || IsBackgroundParameterLoadInProgress)
        {
            return;
        }
        CloseProgressDialog();
        CancelCachedParameterLoad();
        CancelLoadOperation();

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
                if (disposed || !activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId)
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


    /// <summary>
    /// 
    /// </summary>
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
        OnEditSessionChanged();
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
            SetBusy();
            ShowLoadingProgress = true;
            ProgressMessage = "Loading parameters...";
            SetMessages(ProgressMessage);
        });
    }

    /// <summary>
    /// Completes the busy state, resetting all related flags and messages.
    /// </summary>
    protected void CompleteBusyState()
    {
        ProgressMessage = string.Empty;
        ResetBusy();
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
    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }

        DeactivateCore();
        disposed = true;
        Interlocked.Exchange(ref sessionRefreshScheduled, 0);
        CancelCachedParameterLoad();
        CancelLoadOperation();
        base.Dispose();
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        if (disposed)
        {
            return;
        }
        if (activated)
        {
            return;
        }

        activated = true;
        await base.ActivateAsync();

        SetMessages(HasConnection ? null : DefaultStatusMessage, null);
        ResetBusy();
        HasParameters = Parameters.Count > 0;
        activeVehicle.Changed += ActiveVehicleChanged;
        parameterRegistry.Changed += ParameterRegistryChangedAsync;
        parameterLoadStatusSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleParameterLoadStatusChanged>(OnParameterLoadStatusChanged);

        if (activeVehicle.VehicleId is { } vehicleId && HasConnection)
        {
            await ApplyParameterLoadStatusAsync(parameterLoadStatus.Get(vehicleId));
            await ScheduleCachedParameterLoadAsync(vehicleId);
        }

    }

    private void ActiveVehicleChanged(ActiveVehicleChangedEventArgs e)
    {
        OnActiveVehicleChanged(e).FireAndForget();
    }

    private void ParameterRegistryChangedAsync(VehicleParameterChangedEventArgs args)
    {
        OnParameterRegistryChangedAsync(args).FireAndForget();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        DeactivateCore();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void DeactivateCore()
    {
        base.DeactivateCore();
        if (!activated)
        {
            return;
        }
        activated = false;
        activeVehicle.Changed -= ActiveVehicleChanged;
        parameterRegistry.Changed -= ParameterRegistryChangedAsync;
        EditSession?.Changed -= OnEditSessionChanged;
        EditSession = null;
        parameterLoadStatusSubscription.Dispose();
        parameterLoadStatusSubscription = null!;
        CancelCachedParameterLoad();
        Interlocked.Exchange(ref sessionRefreshScheduled, 0);
        CloseProgressDialog();
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
