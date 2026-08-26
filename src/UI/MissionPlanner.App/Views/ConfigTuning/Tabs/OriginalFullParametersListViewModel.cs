using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.Common;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Profiles;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using UraniumUI.Material.Dialogs;
using UraniumUI.Extensions;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Provides the searchable full parameter list through the shared safe editing session.</summary>
public partial class OriginalFullParametersListViewModel : ObservableObject, IDisposable
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
    private readonly ParametersFileHandler parametersFileHandler;
    private readonly IUserConfirmationService confirmation;
    private readonly IParameterProfileRepository profiles;
    private readonly IParameterProfileService profileWorkflow;
    private readonly ILogger<FullParametersListTabViewModel> logger;
    private readonly IDisposable parameterLoadStatusSubscription;
    private CancellationTokenSource? loadCancellation;
    private CancellationTokenSource? cachedLoadCancellation;
    private IParameterEditSession? editSession;
    private ParameterApplyReport? lastApplyReport;
    private IDisposable? progressDialog;
    private bool disposed;
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
    /// <param name="domainFactory">The domain view factory.</param>
    /// <param name="parametersFileHandler">The parameter import/export adapter.</param>
    /// <param name="confirmation">The hazardous-action confirmation service.</param>
    /// <param name="profiles">The named profile repository.</param>
    /// <param name="profileWorkflow">The profile compatibility and staging workflow.</param>
    /// <param name="parameterLoadStatus"></param>
    /// <param name="domainEventHub"></param>
    /// <param name="logger">The logger.</param>
    public OriginalFullParametersListViewModel(
        IVehicleConnectionSession connectionSession,
        IActiveVehicleContext activeVehicle,
        IParameterEditSessionFactory editSessionFactory,
        IDispatcher dispatcher,
        IExtendedDialogService dialogService,
        IDomainFactory domainFactory,
        ParametersFileHandler parametersFileHandler,
        IUserConfirmationService confirmation,
        IParameterProfileRepository profiles,
        IParameterProfileService profileWorkflow,
        IVehicleParameterLoadStatusContext parameterLoadStatus,
        IDomainEventHub domainEventHub,
        ILogger<FullParametersListTabViewModel> logger)
    {
        this.connectionSession = connectionSession;
        parameterRegistry = connectionSession.ParameterRegistry;
        this.activeVehicle = activeVehicle;
        this.editSessionFactory = editSessionFactory;
        this.dispatcher = dispatcher;
        this.dialogService = dialogService;
        this.domainFactory = domainFactory;
        this.parametersFileHandler = parametersFileHandler;
        this.confirmation = confirmation;
        this.profiles = profiles;
        this.profileWorkflow = profileWorkflow;
        this.parameterLoadStatus = parameterLoadStatus;
        this.logger = logger;
        parameterLoadStatusSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleParameterLoadStatusChanged>(OnParameterLoadStatusChanged);
        InitializeView();
    }

    /// <summary>Gets the currently visible parameter rows.</summary>
    public ObservableRangeCollection<ParameterItemViewModel> Parameters { get; } = [];

    /// <summary>Gets the current loading-progress message.</summary>
    [ObservableProperty]
    public partial string? ProgressMessage { get; set; } = null;

    /// <summary>Gets whether parameter loading is in progress.</summary>
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

    /// <summary>Gets whether the active vehicle is disconnected.</summary>
    [ObservableProperty]
    public partial bool ShowVehicleDisconnected
    {
        get; set;
    }

    /// <summary>Gets the number of unapplied parameter values.</summary>
    [ObservableProperty]
    public partial int ModifiedParameterCount
    {
        get; set;
    }

    /// <summary>Gets the total number of loaded parameter fields.</summary>
    [ObservableProperty]
    public partial int TotalParameterCount
    {
        get; set;
    }

    /// <summary>Gets whether a load or apply operation is active.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(WriteParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompareParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(RevertChangesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveToFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveToJsonFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryFailedCommand))]
    public partial bool IsBusy
    {
        get; set;
    }

    /// <summary>Gets whether the connection-owned background parameter download is active.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    public partial bool IsBackgroundParameterLoadInProgress
    {
        get; set;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(WriteParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompareParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(RevertChangesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveToFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveToJsonFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryFailedCommand))]
    public partial bool HasRows
    {
        get; set;
    }

    /// <summary>Gets whether an active vehicle connection is available.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(WriteParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompareParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(RevertChangesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveToFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveToJsonFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetryFailedCommand))]
    public partial bool HasConnection
    {
        get; set;
    }

    /// <summary>Gets the latest editing or apply status.</summary>
    [ObservableProperty]
    public partial string? StatusMessage
    {
        get; set;
    }

    /// <summary>Gets the latest error message.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage
    {
        get; set;
    }

    /// <summary>Gets whether at least one confirmed change requires a vehicle reboot.</summary>
    [ObservableProperty]
    public partial bool RebootRequired
    {
        get; set;
    }

    /// <summary>Activates vehicle lifecycle tracking while the tab is visible.</summary>
    private void InitializeView()
    {
        if (disposed)
        {
            return;
        }

        ErrorMessage = null;
        IsBusy = false;
        HasRows = Parameters.Count > 0;
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

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs vehicleChangedEventArgs)
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
            editSession?.Changed -= OnEditSessionChanged;
            editSession = null;
            Parameters.Clear();
            HasRows = false;
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

    private Task OnParameterLoadStatusChanged(VehicleParameterLoadStatusChanged evt, CancellationToken cancellationToken)
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

    private void ApplyParameterLoadStatus(ParameterLoadStatus? status)
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

    private void OnParameterRegistryChanged(VehicleParameterChangedEventArgs args)
    {
        if (disposed || !activeVehicle.IsOnline || activeVehicle.VehicleId != args.VehicleId || args.Parameter is null)
        {
            return;
        }

        ScheduleCachedParameterLoad(args.VehicleId);
    }

    private void ScheduleCachedParameterLoad(VehicleId vehicleId)
    {
        if (disposed || IsBusy || !HasCompleteCachedParameterSet(vehicleId) || Interlocked.CompareExchange(ref cachedLoadScheduled, 1, 0) != 0)
        {
            return;
        }

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        cachedLoadCancellation = cancellation;
        LoadCachedParametersAsync(vehicleId, cancellation).FireAndForget();
    }

    private bool HasCompleteCachedParameterSet(VehicleId vehicleId)
    {
        var expectedCount = parameterRegistry.GetParameterCount(vehicleId);
        return expectedCount is > 0 &&
               parameterRegistry.GetAllParameters(vehicleId).Count >= expectedCount.Value;
    }

    private async Task LoadCachedParametersAsync(VehicleId vehicleId, CancellationTokenSource cancellation)
    {
        try
        {
            var session = editSessionFactory.Create(vehicleId);
            await session.LoadAsync(cancellationToken: cancellation.Token).ConfigureAwait(false);
            cancellation.Token.ThrowIfCancellationRequested();

            await dispatcher.DispatchAsync(() =>
            {
                if (disposed || !activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId)
                {
                    return;
                }

                AttachSession(session);
                SynchronizeParameterItems();
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
        }
        finally
        {
            Interlocked.CompareExchange(ref cachedLoadCancellation, null, cancellation);
            cancellation.Dispose();
            Interlocked.Exchange(ref cachedLoadScheduled, 0);
        }
    }

    private void CancelCachedParameterLoad()
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

    private async Task HandleLoadError(ParameterStreamResult result, VehicleId vehicleId)
    {
        CloseOperationDialog();
        await dispatcher.DispatchAsync(() =>
        {
            CompleteBusyState();
            ShowLoadingCompletedWithError = true;
            SetMessages(errorMessage: result.ErrorMessage ?? "Parameter loading failed.");
        });
        logger.LogError("Full Parameters List load failed for {VehicleId}: {Error}", vehicleId, result.ErrorMessage);
        HasRows = Parameters.Count > 0;
    }

    private IProgress<ParameterStreamProgress> CreateProgress()
    {
        var progress = new Progress<ParameterStreamProgress>(value => dispatcher.Dispatch(() => ProgressMessage = value.Message ?? (value.TotalCount > 0
            ? $"Processing parameters... {value.ReceivedCount}/{value.TotalCount}"
            : "Processing parameters...")));
        return progress;
    }


    [RelayCommand(CanExecute = nameof(CanRefreshParameters))]
    private async Task ClearParametersAsync(CancellationToken cancellationToken)
    {
        await dispatcher.DispatchAsync(() => Parameters.Clear());
    }

    [RelayCommand(CanExecute = nameof(CanRefreshParameters))]
    private async Task RefreshParametersAsync()
    {
        SetMessages();
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline || IsBackgroundParameterLoadInProgress)
        {
            return;
        }

        CancelCachedParameterLoad();
        CancelLoadOperation();
        loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        var cancellationToken = loadCancellation.Token;
        CloseOperationDialog();
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
                await dialogService.DisplayViewExtendedAsync("Load failed.", view, "OK");
            });
        }
        finally
        {
            CloseOperationDialog();
            loadCancellation?.Dispose();
            loadCancellation = null;
            HasRows = Parameters.Count > 0;
        }
    }

    [RelayCommand]
    private async Task LoadFromEditorAsync(CancellationToken cancellationToken)
    {
        if (editSession is null)
        {
            SetMessages(errorMessage: "Refresh vehicle parameters before importing parameters.");
            return;
        }

        try
        {
            var viewModel = domainFactory.Create<ParametersEditorViewModel>();
            var pageView = domainFactory.Create<ParametersEditorView, ParametersEditorViewModel>(viewModel);

            await dialogService.ShowAsync(pageView, true, cancellationToken);

            //var result = await dialogService.DisplayViewExtendedAsync("Parameters Editor", view, "Add", "Cancel");
            //if (!result)
            //{
            //    SetMessages($"Cancelled");
            //    return;
            //}

            var fullList = editSession.Fields.Select(ToVehicleParameter).ToList();
            var parameters = viewModel.UpdateParameters(fullList);
            foreach (var parameter in parameters)
            {
                editSession.TrySetPending(parameter.Name, parameter.Value, out var _);
            }

            SetMessages($"Imported {parameters.Count} matching values as unapplied edits.");
        }
        catch (Exception exception)
        {
            await dialogService.ConfirmAsync("Load failed", exception.Message, "OK");
        }

        HasRows = Parameters.Count > 0;
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

        HasRows = Parameters.Count > 0;
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

        HasRows = Parameters.Count > 0;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
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

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveToJsonFileAsync()
    {
        try
        {
            var result = await parametersFileHandler.SaveParametersToJsonFile(Parameters, CancellationToken.None);
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

        try
        {
            using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken);
            var plan = editSession.CreateWritePlan();
            var preview = string.Join(Environment.NewLine, plan.Entries.Select(entry => $"{entry.DisplayName} ({entry.Name}): {entry.LiveValue:R} → {entry.PendingValue:R} {entry.Units}".TrimEnd()));

            var rebootCount = plan.Entries.Count(entry => entry.RebootRequired);
            var accepted = await confirmation.ConfirmAsync(
                "Review parameter writes",
                $"{preview}{Environment.NewLine}{Environment.NewLine}{rebootCount} change(s) require reboot.",
                $"Write {plan.Entries.Count} parameters",
                connectionCancellation.Token);
            if (!accepted)
            {
                logger.LogInformation("Parameter write plan was cancelled for {VehicleId}.", editSession.VehicleId);
                SetMessages("Parameter write cancelled. No values were sent.");
                return;
            }

            IsBusy = true;
            SetMessages($"Applying {plan.Entries.Count} modified parameters...");
            var progress = new Progress<ParameterApplyProgress>(value =>
                dispatcher.Dispatch(() => ProgressMessage = $"{value.Index}/{value.Total}: {value.Name} — {value.Message}"));


            var report = await editSession.ApplyAsync(plan, progress, connectionCancellation.Token);

            lastApplyReport = report;
            RebootRequired |= report.RebootRequired;
            var statusMessage = report.Success ? $"Confirmed {report.Confirmed.Count} parameter changes by vehicle readback." : null;
            var errorMessage = report.Success ? null : BuildResultSummary(report);

            SetMessages(statusMessage, errorMessage);
        }
        catch (OperationCanceledException)
        {
            SetMessages(null, "Parameter apply was cancelled before all values were confirmed.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to apply Full Parameters List edits.");
            SetMessages(null, exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCompareParameters))]
    private async Task CompareParametersAsync(CancellationToken cancellationToken)
    {
        if (editSession is null)
        {
            return;
        }

        var viewModel = domainFactory.Create<ParameterComparisonViewModel, IParameterEditSession>(editSession);
        var pageView = domainFactory.Create<ParameterComparisonView, ParameterComparisonViewModel>(viewModel);
        await dialogService.ShowAsync(pageView, true, cancellationToken);
    }

    private void OnComparisonStaged(object? sender, int count)
    {
        SetMessages($"Staged {count} safe differences as pending edits. No values were written.");
    }

    [RelayCommand]
    private async Task LoadPreSavedAsync(CancellationToken cancellationToken)
    {
        var saved = await profiles.GetAllAsync(cancellationToken);
        if (saved.Count == 1 && editSession is not null)
        {
            var review = profileWorkflow.Review(saved[0], editSession);
            var safe = review.Comparison.Rows.Where(row => row.CanStage).Select(row => row.Name).ToArray();
            var warning = review.Warnings.Count == 0
                ? string.Empty
                : Environment.NewLine + string.Join(Environment.NewLine, review.Warnings);
            var accepted = await confirmation.ConfirmAsync(
                $"Stage profile: {saved[0].Name}",
                $"{safe.Length} compatible difference(s) can be staged. Unsupported, invalid, absent, and read-only entries will remain unstaged.{warning}",
                $"Stage {safe.Length} values",
                cancellationToken);
            if (accepted)
            {
                var staged = profileWorkflow.Stage(review, editSession, safe);
                SetMessages($"Staged {staged.Count} profile values as unapplied edits. Review and apply them separately.");
            }

            return;
        }

        await dialogService.ConfirmAsync(
            "Parameter profiles",
            saved.Count == 0
                ? "No named parameter profiles have been saved."
                : string.Join(Environment.NewLine, saved.Select(profile => $"{profile.Name} — {profile.Values.Count} values — {profile.UpdatedAt:g}")),
            "OK");
    }

    [RelayCommand(CanExecute = nameof(CanRetryFailed))]
    private async Task RetryFailedAsync(CancellationToken cancellationToken)
    {
        if (editSession is null || lastApplyReport is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var retry = await editSession.RetryFailedAsync(lastApplyReport, cancellationToken: cancellationToken);
            lastApplyReport = retry;
            RebootRequired |= retry.RebootRequired;
            SetMessages(retry.Success ? $"Confirmed {retry.Confirmed.Count} retried changes." : null, retry.Success ? null : BuildResultSummary(retry));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRevertChanges))]
    private void RevertChanges()
    {
        editSession?.RevertAll();
        SetMessages("All unapplied values were reverted to current live values.");
    }

    [RelayCommand(CanExecute = nameof(CanCancelLoad))]
    private void CancelLoad()
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

    private bool CanRefreshParameters()
    {
        return HasConnection && !IsBusy && !IsBackgroundParameterLoadInProgress;
    }

    private bool CanRevertChanges()
    {
        return HasConnection && HasRows && editSession is { IsDirty: true, IsValid: true };
    }

    private bool CanCompareParameters()
    {
        return HasConnection && HasRows;
    }

    private bool CanSave()
    {
        return HasConnection && !IsBusy && HasRows;
    }

    private bool CanCancelLoad()
    {
        return IsBusy;
    }

    private bool CanWriteParameters()
    {
        return HasConnection && !IsBusy && editSession is { IsDirty: true, IsValid: true };
    }

    private bool CanRetryFailed()
    {
        return HasConnection && !IsBusy && editSession is { IsValid: true } && lastApplyReport?.Retryable.Count > 0;
    }

    private static string BuildResultSummary(ParameterApplyReport report)
    {
        return string.Join(
            "; ",
            report.Results
                .GroupBy(result => result.Outcome)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key}: {group.Count()}"));
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

    private void OnEditSessionChanged()
    {
        if (disposed ||
            Interlocked.Exchange(ref sessionRefreshScheduled, 1) != 0)
        {
            return;
        }

        if (!dispatcher.Dispatch(() =>
            {
                Interlocked.Exchange(ref sessionRefreshScheduled, 0);

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
            }))
        {
            Interlocked.Exchange(ref sessionRefreshScheduled, 0);
        }
    }

    private void SynchronizeParameterItems(IProgress<ParameterStreamProgress>? progress = null)
    {
        if (editSession is null)
        {
            return;
        }

        var fields = editSession.Fields;
        progress?.Report(new ParameterStreamProgress(Message: $"Creating data grid for {fields.Count} parameters"));

        var itemsByName = Parameters.ToDictionary(item => item.Name, StringComparer.Ordinal);
        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        var structureChanged = false;
        var nextItems = new List<ParameterItemViewModel>(fields.Count);
        foreach (var field in fields)
        {
            fieldNames.Add(field.Name);
            if (itemsByName.TryGetValue(field.Name, out var item))
            {
                item.SetField(field);
                nextItems.Add(item);
            }
            else
            {
                nextItems.Add(new ParameterItemViewModel(editSession, field));
                structureChanged = true;
            }
        }

        if (Parameters.Any(item => !fieldNames.Contains(item.Name)))
        {
            structureChanged = true;
        }

        if (structureChanged)
        {
            nextItems.Sort((left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
            Parameters.ReplaceRange(nextItems);
        }

        TotalParameterCount = Parameters.Count;
        ModifiedParameterCount = fields.Count(field => field.IsModified);

        WriteParametersCommand.NotifyCanExecuteChanged();
        RetryFailedCommand.NotifyCanExecuteChanged();
        HasRows = Parameters.Count > 0;
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

    /// <inheritdoc />
    public void Dispose()
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

        editSession?.Changed -= OnEditSessionChanged;
        editSession = null;

        // The page is retained by Shell even though this view model is transient.
        // Release the large row graph immediately so recycled editor controls and
        // parameter metadata do not remain rooted while another page is active.
        Parameters.Clear();
        lastApplyReport = null;
        HasRows = false;
        TotalParameterCount = 0;
        ModifiedParameterCount = 0;
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
