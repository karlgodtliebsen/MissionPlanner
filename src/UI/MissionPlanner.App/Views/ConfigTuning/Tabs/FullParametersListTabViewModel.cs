using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.Common;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Profiles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Parameters;
using UraniumUI.Material.Dialogs;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Provides the searchable full parameter list through the shared safe editing session.</summary>
public partial class FullParametersListTabViewModel : ParametersViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IDispatcher dispatcher;
    private readonly IExtendedDialogService dialogService;
    private readonly IDomainFactory domainFactory;
    private readonly ParametersFileHandler parametersFileHandler;
    private readonly IUserConfirmationService confirmation;
    private readonly IParameterProfileRepository profiles;
    private readonly IParameterProfileService profileWorkflow;
    private readonly ILogger<ParametersViewModel> logger;
    private ParameterApplyReport? lastApplyReport;
    private bool disposed;
    private int sessionRefreshScheduled;


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
    public FullParametersListTabViewModel(
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
        ILogger<FullParametersListTabViewModel> logger) : base(connectionSession, activeVehicle, editSessionFactory, dispatcher, dialogService, domainFactory, parameterLoadStatus, domainEventHub, logger)
    {
        this.activeVehicle = activeVehicle;
        this.dispatcher = dispatcher;
        this.dialogService = dialogService;
        this.domainFactory = domainFactory;
        this.parametersFileHandler = parametersFileHandler;
        this.confirmation = confirmation;
        this.profiles = profiles;
        this.profileWorkflow = profileWorkflow;
        this.logger = logger;
    }

    /// <summary>Gets whether a load or apply operation is active.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(WriteParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompareParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(RevertChangesCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveToFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveToJsonFileCommand))]
    //[NotifyCanExecuteChangedFor(nameof(ClearParametersCommand))]
    //[NotifyCanExecuteChangedFor(nameof(RetryFailedCommand))]
    public new partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(WriteParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompareParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(RevertChangesCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveToFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveToJsonFileCommand))]
    //[NotifyCanExecuteChangedFor(nameof(ClearParametersCommand))]
    //[NotifyCanExecuteChangedFor(nameof(RetryFailedCommand))]
    public partial bool HasRows { get; set; }

    /// <summary>Gets whether at least one confirmed change requires a vehicle reboot.</summary>
    [ObservableProperty]
    public partial bool RebootRequired { get; set; }


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

        await dialogService.ConfirmAsync("Parameter profiles", saved.Count == 0
            ? "No named parameter profiles have been saved."
            : string.Join(Environment.NewLine, saved.Select(profile => $"{profile.Name} — {profile.Values.Count} values — {profile.UpdatedAt:g}")), "OK");
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

    private bool CanWriteParameters()
    {
        return HasConnection && !IsBusy && editSession is { IsDirty: true, IsValid: true };
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

    /// <inheritdoc />
    protected override void OnEditSessionChanged(object? sender, EventArgs args)
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
        // RetryFailedCommand.NotifyCanExecuteChanged();
        HasRows = Parameters.Count > 0;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }

        base.Dispose();
        disposed = true;

        Interlocked.Exchange(ref sessionRefreshScheduled, 0);
        CancelLoadOperation();
        lastApplyReport = null;
        HasRows = false;
    }

    private static VehicleParameter ToVehicleParameter(ParameterEditField field)
    {
        return new VehicleParameter(field.Name, (float)field.PendingValue, field.Type, 0, 0);
    }
}
