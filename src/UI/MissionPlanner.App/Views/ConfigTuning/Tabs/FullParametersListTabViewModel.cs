using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Common;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Comparison;
using MissionPlanner.Core.ConfigTuning.Profiles;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Parameters;
using UraniumUI.Dialogs;
using UraniumUI.Material.Dialogs;

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
    private readonly IUserConfirmationService confirmation;
    private readonly IParameterComparisonService comparisons;
    private readonly IParameterProfileRepository profiles;
    private readonly IParameterProfileService profileWorkflow;
    private readonly ILogger<FullParametersListTabViewModel> logger;
    private CancellationTokenSource? loadCancellation;
    private IParameterEditSession? editSession;
    private ParameterApplyReport? lastApplyReport;
    private ParameterComparisonResult? comparisonResult;
    private readonly List<ParameterComparisonItemViewModel> allComparisonRows = [];
    private IDisposable? progressDialog;
    private bool disposed;

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
    /// <param name="confirmation">The hazardous-action confirmation service.</param>
    /// <param name="comparisons">The parameter comparison engine.</param>
    /// <param name="profiles">The named profile repository.</param>
    /// <param name="profileWorkflow">The profile compatibility and staging workflow.</param>
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
        IUserConfirmationService confirmation,
        IParameterComparisonService comparisons,
        IParameterProfileRepository profiles,
        IParameterProfileService profileWorkflow,
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
        this.confirmation = confirmation;
        this.comparisons = comparisons;
        this.profiles = profiles;
        this.profileWorkflow = profileWorkflow;
        this.logger = logger;
        InitializeView();
    }

    /// <summary>Gets the currently visible parameter rows.</summary>
    public ObservableRangeCollection<ParameterItemViewModel> Parameters { get; } = [];

    /// <summary>Gets the currently filtered comparison rows.</summary>
    public ObservableRangeCollection<ParameterComparisonItemViewModel> ComparisonRows { get; } = [];

    /// <summary>Gets the available comparison status filters.</summary>
    public IReadOnlyList<string> ComparisonFilters { get; } = ["Differences", "Missing", "Invalid", "Modified", "All"];

    /// <summary>Gets whether the comparison workspace is visible.</summary>
    [ObservableProperty]
    public partial bool ShowComparison { get; set; }

    /// <summary>Gets or sets the comparison status filter.</summary>
    [ObservableProperty]
    public partial string ComparisonFilter { get; set; } = "Differences";

    partial void OnComparisonFilterChanged(string value) => FilterComparisonRows();

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
    public partial bool IsBusy { get; set; }

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
    public partial bool HasRows { get; set; }

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
        HasConnection = activeVehicle.IsOnline;
        ShowVehicleDisconnected = !HasConnection;
        StatusMessage = HasConnection ? null : DefaultStatusMessage;
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs vehicleChangedEventArgs)
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
            HasConnection = changed;
            ShowVehicleDisconnected = !changed;
            CancelLoadOperation();
            CloseProgressDialog();
            CompleteBusyState();

            var statusMessage = changed ? "Vehicle changed. Refresh parameters." : null;
            var errorMessage = changed ? null : "The vehicle is disconnected.";
            Debug.Assert(statusMessage is null || errorMessage is null);
            SetMessages(statusMessage, errorMessage);
        });
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
    private async Task ClearParametersAsync()
    {
        await dispatcher.DispatchAsync(() => Parameters.Clear());
    }

    [RelayCommand(CanExecute = nameof(CanRefreshParameters))]
    private async Task RefreshParametersAsync()
    {
        SetMessages();
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
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
            progressDialog = await extendedDialogService.DisplayProgressCancellableAsync("Loading parameters", () => ProgressMessage, tokenSource: loadCancellation);
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
                await dialogService.DisplayViewAsync("Load failed.", view, "OK");
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
            var preview = string.Join(
                Environment.NewLine,
                plan.Entries.Select(entry =>
                    $"{entry.DisplayName} ({entry.Name}): {entry.LiveValue:R} → {entry.PendingValue:R} {entry.Units}".TrimEnd()));
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
            var statusMessage = report.Success
                ? $"Confirmed {report.Confirmed.Count} parameter changes by vehicle readback."
                : null;
            var errorMessage = report.Success
                ? null
                : BuildResultSummary(report);

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
    private void CompareParameters()
    {
        if (editSession is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var firmware = editSession.Scope.FirmwareIdentity;
        var live = editSession.Fields.ToDictionary(
            field => field.Name,
            field => new ParameterComparisonInput(field.Name, field.LiveValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
            StringComparer.Ordinal);
        var pending = editSession.Fields.ToDictionary(
            field => field.Name,
            field => new ParameterComparisonInput(field.Name, field.PendingValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
            StringComparer.Ordinal);
        var metadata = editSession.Fields.ToDictionary(field => field.Name, field => field.Metadata, StringComparer.Ordinal);
        var result = comparisons.Compare(
            new ParameterComparisonSource("Live", editSession.VehicleId.ToString(), now, firmware),
            live,
            new ParameterComparisonSource("Pending", editSession.VehicleId.ToString(), now, firmware),
            pending,
            metadata);
        comparisonResult = result;
        allComparisonRows.Clear();
        allComparisonRows.AddRange(result.Rows.Select(row => new ParameterComparisonItemViewModel(row)));
        FilterComparisonRows();
        ShowComparison = true;
        SetMessages($"Comparing {result.Left.Name} with {result.Right.Name} from {result.Right.Timestamp:g}.", result.Warning);
    }

    [RelayCommand]
    private void CloseComparison()
    {
        ShowComparison = false;
    }

    [RelayCommand]
    private void SelectAllSafeDifferences()
    {
        foreach (var row in allComparisonRows)
        {
            row.IsSelected = row.CanStage;
        }
    }

    [RelayCommand]
    private void StageSelectedDifferences()
    {
        if (comparisonResult is null || editSession is null)
        {
            return;
        }

        var selected = allComparisonRows.Where(row => row.IsSelected).Select(row => row.Name).ToArray();
        var staged = comparisons.Stage(comparisonResult, editSession, selected);
        ShowComparison = false;
        SetMessages($"Staged {staged.Count} safe differences as pending edits. No values were written.");
    }

    [RelayCommand]
    private async Task ExportComparisonJsonAsync(CancellationToken cancellationToken)
    {
        if (comparisonResult is not null)
        {
            await parametersFileHandler.SaveTextFileAsync("parameter-comparison.json", comparisons.ExportJson(comparisonResult), cancellationToken);
        }
    }

    [RelayCommand]
    private async Task ExportComparisonCsvAsync(CancellationToken cancellationToken)
    {
        if (comparisonResult is not null)
        {
            await parametersFileHandler.SaveTextFileAsync("parameter-comparison.csv", comparisons.ExportCsv(comparisonResult), cancellationToken);
        }
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
        return HasConnection && !IsBusy;
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

    private bool CanRetryFailed() =>
        HasConnection && !IsBusy && editSession is { IsValid: true } && lastApplyReport?.Retryable.Count > 0;

    private static string BuildResultSummary(ParameterApplyReport report) =>
        string.Join(
            "; ",
            report.Results
                .GroupBy(result => result.Outcome)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key}: {group.Count()}"));

    private void FilterComparisonRows()
    {
        IEnumerable<ParameterComparisonItemViewModel> rows = allComparisonRows;
        rows = ComparisonFilter switch
        {
            "Differences" => rows.Where(row => row.Status is not ParameterComparisonStatus.Equal),
            "Missing" => rows.Where(row => row.Status is ParameterComparisonStatus.OnlyOnLeft or ParameterComparisonStatus.OnlyOnRight or ParameterComparisonStatus.MetadataMissing),
            "Invalid" => rows.Where(row => row.Status is ParameterComparisonStatus.InvalidRightValue or ParameterComparisonStatus.ReadOnly),
            "Modified" => rows.Where(row => row.CanStage),
            _ => rows
        };
        ComparisonRows.ReplaceRange(rows);
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

    //private void SetDisconnectedState()
    //{
    //    HasConnection = false;
    //    ShowVehicleDisconnected = true;
    //    SetMessages(null, DefaultStatusMessage);
    //    CloseProgressDialog();
    //    CompleteBusyState();
    //}

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
        activeVehicle.Changed -= OnActiveVehicleChanged;
        CancelLoadOperation();
        editSession?.Changed -= OnEditSessionChanged;
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
