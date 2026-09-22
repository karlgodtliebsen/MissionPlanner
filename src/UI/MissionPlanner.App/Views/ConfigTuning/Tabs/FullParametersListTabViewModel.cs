using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Models;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dialogs.SubViews;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Profiles;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Parameters;
using Ursa.Controls;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Provides the searchable full parameter list through the shared safe editing session.</summary>
public partial class FullParametersListTabViewModel : ParametersViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly ITextClipboardService clipboard;
    private readonly IDialogService dialogService;
    private readonly IDomainFactory domainFactory;
    private readonly ParametersFileHandler parametersFileHandler;
    private readonly IUserConfirmationService confirmation;
    private readonly IParameterProfileRepository profiles;
    private readonly IParameterProfileService profileWorkflow;
    private ParameterApplyReport? lastApplyReport;
    private bool disposed;
    private bool pageActive;
    private readonly IVehicleConnectionService connections;
    private CancellationTokenSource? reconnectCancellation;
    private int sessionRefreshScheduled;


    /// <summary>Initializes the Full Parameters List tab.</summary>
    /// <param name="connectionSession">The current connection-scoped services.</param>
    /// <param name="activeVehicle">The application active-vehicle context.</param>
    /// <param name="editSessionFactory">The shared parameter editing-session factory.</param>
    /// <param name="clipboard"></param>
    /// <param name="dialogService">The extended dialog service.</param>
    /// <param name="domainFactory">The domain view factory.</param>
    /// <param name="parametersFileHandler">The parameter import/export adapter.</param>
    /// <param name="confirmation">The hazardous-action confirmation service.</param>
    /// <param name="profiles">The named profile repository.</param>
    /// <param name="profileWorkflow">The profile compatibility and staging workflow.</param>
    /// <param name="parameterLoadStatus"></param>
    /// <param name="domainEventHub"></param>
    /// <param name="connections">Captures and reconnects the current transport.</param>
    /// <param name="logger">The logger.</param>
    public FullParametersListTabViewModel(
        IDialogService dialogService,
        IDomainFactory domainFactory,
        IDomainEventHub domainEventHub,
        IVehicleConnectionSession connectionSession,
        IActiveVehicleContext activeVehicle,
        IParameterEditSessionFactory editSessionFactory,
        ITextClipboardService clipboard,
        ParametersFileHandler parametersFileHandler,
        IUserConfirmationService confirmation,
        IParameterProfileRepository profiles,
        IParameterProfileService profileWorkflow,
        IVehicleParameterLoadStatusContext parameterLoadStatus,
        ILogger<FullParametersListTabViewModel> logger,
        IVehicleConnectionService connections)
        : base(connectionSession, activeVehicle, editSessionFactory, dialogService, domainFactory, parameterLoadStatus, domainEventHub, logger)
    {
        this.connections = connections;
        this.activeVehicle = activeVehicle;
        this.clipboard = clipboard;
        this.dialogService = dialogService;
        this.domainFactory = domainFactory;
        this.parametersFileHandler = parametersFileHandler;
        this.confirmation = confirmation;
        this.profiles = profiles;
        this.profileWorkflow = profileWorkflow;
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        if (disposed)
        {
            return;
        }
        pageActive = true;
        PropertyChanged += OnViewModelPropertyChanged;
        await base.ActivateAsync();
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        if (disposed)
        {
            return;
        }
        pageActive = false;
        reconnectCancellation?.Cancel();
        PropertyChanged -= OnViewModelPropertyChanged;
        await base.DeactivateAsync();
        Interlocked.Exchange(ref sessionRefreshScheduled, 0);
        CancelLoadOperation();
        lastApplyReport = null;
        HasRows = false;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        if (disposed)
        {
            return;
        }
        pageActive = false;
        reconnectCancellation?.Cancel();
        base.Dispose();
        disposed = true;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(WriteParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CompareParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(RevertChangesCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveToFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveToJsonFileCommand))]
    public partial bool HasRows
    {
        get; set;
    }

    /// <summary>Gets whether at least one confirmed change requires a vehicle reboot.</summary>
    [ObservableProperty]
    public partial bool RebootRequired
    {
        get; set;
    }

    private string CreateTextExport()
    {
        if (EditSession is null)
        {
            SetMessages(errorMessage: "Refresh vehicle parameters before importing parameters.");
            NotificationManager?.Show(ErrorMessage ?? "");
            return string.Empty;
        }

        var fullList = EditSession.Fields.Select(ToVehicleParameter).ToList();
        return string.Join(Environment.NewLine, fullList.Select(item => $"{item.Name}={item.Value}"));
    }

    [RelayCommand]
    private async Task CopyAllAsync()
    {
        if (EditSession is null)
        {
            SetMessages(errorMessage: "Refresh vehicle parameters before importing parameters.");
            return;
        }
        await clipboard.SetTextAsync(CreateTextExport());
        SetMessages($"Copied {EditSession.Fields.Count} parameters.");
        NotificationManager?.Show(StatusMessage ?? "");
    }

    [RelayCommand]
    private async Task UseQuickEditorAsync(CancellationToken cancellationToken)
    {
        if (EditSession is null)
        {
            SetMessages(errorMessage: "Refresh vehicle parameters before importing parameters.");
            NotificationManager?.Show(ErrorMessage ?? "");
            return;
        }
        try
        {
            var viewModel = domainFactory.Create<ParametersEditorViewModel>();
            var options = dialogService.CreateOptions("", null, null);
            options.FullScreen = false;
            options.CanDragMove = true;
            options.CanResize = true;
            options.Buttons = DialogButton.OKCancel;

            var returnModel = await dialogService.ShowStandardAsync<ParametersEditorView, ParametersEditorViewModel>(viewModel, options, cancellationToken: cancellationToken);
            if (returnModel is not null)
            {
                UpdateEditSession(returnModel);
            }
        }
        catch (Exception exception)
        {
            var viewModel = domainFactory.Create<ErrorViewModel, string>(exception.Message + "\nEnsure there is a connection and try again");
            var options = dialogService.CreateOptions("Load failed", "Ok", null);
            await dialogService.ShowCustomDialogAsync<ErrorView, ErrorViewModel>(viewModel, options, cancellationToken: cancellationToken);
        }

        HasRows = Parameters.Count > 0;
    }

    private void UpdateEditSession(ParametersEditorViewModel vm)
    {
        DomainException.ThrowIfNull(EditSession);
        var fullList = EditSession.Fields.Select(ToVehicleParameter).ToList();
        var parameters = vm.UpdateParameters(fullList);
        if (parameters.Count == 0)
        {
            SetMessages($"No valid parameters where found.");
            NotificationManager?.Show(StatusMessage ?? "");
            return;
        }

        using var notifications = EditSession.DeferChangeNotifications();
        foreach (var parameter in parameters)
        {
            EditSession.TrySetPending(parameter.Name, parameter.Value, out var _);
        }

        SetMessages($"Imported {parameters.Count} matching values as unapplied edits.");
        NotificationManager?.Show(StatusMessage ?? "");
        HasRows = Parameters.Count > 0;
    }


    [RelayCommand]
    private async Task LoadFromFileAsync(CancellationToken cancellationToken)
    {
        if (EditSession is null)
        {
            SetMessages(errorMessage: "Refresh vehicle parameters before importing a parameter file.");
            NotificationManager?.Show(ErrorMessage ?? "");
            return;
        }

        try
        {
            var loaded = await parametersFileHandler.LoadParametersFromFileAsync(
                EditSession.Fields.Select(ToVehicleParameter).ToList(),
                activeVehicle.ConnectionCancellationToken);
            using var notifications = EditSession.DeferChangeNotifications();
            foreach (var parameter in loaded)
            {
                EditSession.TrySetPending(parameter.Name, parameter.Value, out var _);
            }

            SetMessages($"Imported {loaded.Count} matching values as unapplied edits.");
            NotificationManager?.Show(StatusMessage ?? "");
        }
        catch (Exception exception)
        {
            var viewModel = domainFactory.Create<ErrorViewModel, string>(exception.Message + "\nEnsure there is a connection and try again");
            var options = dialogService.CreateOptions("Load from file failed", "Ok", null);
            var result = await dialogService.ShowCustomDialogAsync<ErrorView, ErrorViewModel>(viewModel, options, cancellationToken: cancellationToken);
        }

        HasRows = Parameters.Count > 0;
    }

    [RelayCommand]
    private async Task LoadFromJsonFileAsync(CancellationToken cancellationToken)
    {
        if (EditSession is null)
        {
            SetMessages(errorMessage: "Refresh vehicle parameters before importing a parameter file.");
            NotificationManager?.Show(ErrorMessage ?? "");
            return;
        }

        try
        {
            var loaded = await parametersFileHandler.LoadParametersFromJsonFileAsync(activeVehicle.ConnectionCancellationToken);
            using var notifications = EditSession.DeferChangeNotifications();
            foreach (var parameter in loaded)
            {
                EditSession.TrySetPending(parameter.Name, parameter.Value, out var _);
            }

            SetMessages($"Imported {loaded.Count} matching values as unapplied edits.");
            NotificationManager?.Show(StatusMessage ?? "");
        }
        catch (Exception exception)
        {
            var viewModel = domainFactory.Create<ErrorViewModel, string>(exception.Message + "\nEnsure there is a connection and try again");
            var options = dialogService.CreateOptions("Load from Json file failed", "Ok", null);
            var result = await dialogService.ShowCustomDialogAsync<ErrorView, ErrorViewModel>(viewModel, options, cancellationToken: cancellationToken);
        }

        HasRows = Parameters.Count > 0;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveToFileAsync(CancellationToken cancellationToken)
    {
        try
        {
            var parameters = EditSession?.Fields.Select(ToVehicleParameter).ToList() ?? [];
            var result = await parametersFileHandler.SaveParametersToFile(parameters, cancellationToken);
            if (result is not null)
            {
                NotificationManager?.Show($"File saved to:\n{result}\nfor Vehicle: {activeVehicle.VehicleId}");
            }
        }
        catch (Exception exception)
        {
            await ShowMessageAsync("Save failed", exception.Message, cancellationToken);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveToJsonFileAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await parametersFileHandler.SaveParametersToJsonFile(Parameters, cancellationToken);
            if (result is not null)
            {
                NotificationManager?.Show($"File saved to:\n{result}\nfor Vehicle: {activeVehicle.VehicleId}");
            }
        }
        catch (Exception exception)
        {
            await ShowMessageAsync("Save failed", exception.Message, cancellationToken);
        }
    }

    [RelayCommand(CanExecute = nameof(CanWriteParameters))]
    private async Task WriteParametersAsync(CancellationToken cancellationToken)
    {
        if (EditSession is null)
        {
            return;
        }

        try
        {
            using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken);
            var reconnectTarget = connections.CaptureReconnectTarget();
            var plan = EditSession.CreateWritePlan();
            var preview = string.Join(Environment.NewLine, plan.Entries.Select(entry => $"{entry.DisplayName} ({entry.Name}): {entry.LiveValue:R} → {entry.PendingValue:R} {entry.Units}".TrimEnd()));
            var skippedPreview = plan.Skipped.Count == 0
                ? string.Empty
                : $"{Environment.NewLine}{Environment.NewLine}Skipped {plan.Skipped.Count} unsafe change(s):{Environment.NewLine}" +
                  string.Join(Environment.NewLine, plan.Skipped.Select(item => $"{item.Name}: {item.Message}"));

            var rebootCount = plan.Entries.Count(entry => entry.RebootRequired);
            if (plan.Entries.Count == 0)
            {
                SetMessages(errorMessage: $"No safe modified parameters can be written. {BuildResultSummary(new ParameterApplyReport(false, plan.Skipped, false))}");
                NotificationManager?.Show(ErrorMessage ?? "");
                return;
            }
            var accepted = await confirmation.ConfirmAsync(
                "Review parameter writes",
                $"{preview}{skippedPreview}{Environment.NewLine}{Environment.NewLine}{rebootCount} change(s) require reboot.",
                $" {plan.Entries.Count} parameters",
                connectionCancellation.Token);
            if (!accepted)
            {
                Logger.LogInformation("Parameter write plan was cancelled for {VehicleId}.", EditSession.VehicleId);
                SetMessages("Parameter write cancelled. No values were sent.");
                NotificationManager?.Show(StatusMessage ?? "");
                return;
            }

            SetBusy();
            SetMessages($"Applying {plan.Entries.Count} modified parameters...");
            var progress = new Progress<ParameterApplyProgress>(value =>
                Dispatcher.Dispatch(() => ProgressMessage = $"{value.Index}/{value.Total}: {value.Name} — {value.Message}"));

            await Dispatcher.DispatchAsync(async () =>
            {
                var report = await EditSession.ApplyAsync(plan, progress, connectionCancellation.Token);
                lastApplyReport = report;
                RebootRequired |= report.RebootRequired;
                var statusMessage = report.Success ? $"Confirmed {report.Confirmed.Count} parameter changes by vehicle readback." : null;
                var errorMessage = report.Success ? null : BuildResultSummary(report);
                SetMessages(statusMessage, errorMessage);
                NotificationManager?.Show(StatusMessage ?? "");
                NotificationManager?.Show(ErrorMessage ?? "");
                if (report.RebootRequired)
                {
                    await ReconnectAfterApplyAsync(reconnectTarget, cancellationToken);
                }
            });


        }
        catch (OperationCanceledException)
        {
            SetMessages(null, "Parameter apply was cancelled before all values were confirmed.");
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to apply Full Parameters List edits.");
            SetMessages(null, exception.Message);
        }
        finally
        {
            ResetBusy();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCompareParameters))]
    private async Task CompareParametersAsync(CancellationToken cancellationToken)
    {
        if (EditSession is null)
        {
            return;
        }

        var viewModel = domainFactory.Create<ParameterComparisonViewModel, IParameterEditSession>(EditSession);

        var options = dialogService.CreateOptions("Compare parameters", "Close", null);
        options.FullScreen = true;
        await dialogService.ShowCustomDialogAsync<ParameterComparisonView, ParameterComparisonViewModel>(
            viewModel,
            options,
            cancellationToken: cancellationToken);
    }

    private void OnComparisonStaged(object? sender, int count)
    {
        SetMessages($"Staged {count} safe differences as pending edits. No values were written.");
    }

    [RelayCommand]
    private async Task LoadPreSavedAsync(CancellationToken cancellationToken)
    {
        var saved = await profiles.GetAllAsync(cancellationToken);
        if (saved.Count == 1 && EditSession is not null)
        {
            var review = profileWorkflow.Review(saved[0], EditSession);
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
                var staged = profileWorkflow.Stage(review, EditSession, safe);
                SetMessages($"Staged {staged.Count} profile values as unapplied edits. Review and apply them separately.");
                NotificationManager?.Show(StatusMessage ?? "");
            }

            return;
        }

        await ShowMessageAsync("Parameter profiles", saved.Count == 0
            ? "No previously saved parameter profiles have been found."
            : string.Join(Environment.NewLine, saved.Select(profile => $"{profile.Name} — {profile.Values.Count} values — {profile.UpdatedAt:g}")),
            cancellationToken);
    }

    /// <inheritdoc />
    protected override bool CanCancelLoad()
    {
        return IsBackgroundParameterLoadInProgress;
    }

    private async Task<bool> ShowMessageAsync(string title, string message, CancellationToken cancellationToken)
    {
        var options = dialogService.CreateOptions(title, "Ok", null);
        var result = await dialogService.ConfirmAsync(options, message, cancellationToken);
        return result;
    }


    [RelayCommand(CanExecute = nameof(CanRetryFailed))]
    private async Task RetryFailedAsync(CancellationToken cancellationToken)
    {
        if (EditSession is null || lastApplyReport is null)
        {
            return;
        }

        SetBusy();
        try
        {
            var reconnectTarget = connections.CaptureReconnectTarget();
            var retry = await EditSession.RetryFailedAsync(lastApplyReport, cancellationToken: cancellationToken);
            lastApplyReport = retry;
            RebootRequired |= retry.RebootRequired;
            SetMessages(retry.Success ? $"Confirmed {retry.Confirmed.Count} retried changes." : null, retry.Success ? null : BuildResultSummary(retry));
            NotificationManager?.Show(StatusMessage ?? "");
            NotificationManager?.Show(ErrorMessage ?? "");
            if (retry.RebootRequired)
            {
                await ReconnectAfterApplyAsync(reconnectTarget, cancellationToken);
            }
        }
        finally
        {
            ResetBusy();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRevertChanges))]
    private void RevertChanges()
    {
        EditSession?.RevertAll();
        SetMessages("All unapplied values were reverted to current live values.");
    }

    private bool CanRevertChanges()
    {
        return HasConnection && HasRows && !IsBusy && EditSession is { IsDirty: true, IsValid: true };
    }

    private bool CanCompareParameters()
    {
        return HasConnection && HasRows && !IsBusy && EditSession is { IsDirty: true, IsValid: true };
    }

    private bool CanSave()
    {
        return HasConnection && !IsBusy && HasRows;
    }

    private bool CanWriteParameters()
    {
        return HasConnection && HasRows && !IsBusy && EditSession is { IsDirty: true, IsValid: true };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(IsBusy) or nameof(HasConnection) or nameof(HasParameters) or nameof(HasRows)))
        {
            return;
        }
        UpdateCommandState();
    }

    /// <inheritdoc />
    protected override void OnEditSessionSynchronized()
    {
        base.OnEditSessionSynchronized();
        UpdateCommandState();
    }

    private void UpdateCommandState()
    {
        WriteParametersCommand.NotifyCanExecuteChanged();
        CompareParametersCommand.NotifyCanExecuteChanged();
        RevertChangesCommand.NotifyCanExecuteChanged();
        SaveToFileCommand.NotifyCanExecuteChanged();
        SaveToJsonFileCommand.NotifyCanExecuteChanged();
        HasRows = Parameters.Count > 0;
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

    private static VehicleParameter ToVehicleParameter(ParameterEditField field)
    {
        return new VehicleParameter(field.Name, (float)field.PendingValue, field.Type, 0, 0);
    }
}
