using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Presentation;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.AvaloniaUI.App.Views.Common;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Profiles;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Parameters;
using ErrorView = MissionPlanner.AvaloniaUI.App.Utilities.Dialogs.SubViews.ErrorView;
using ErrorViewModel = MissionPlanner.AvaloniaUI.App.Utilities.Dialogs.SubViews.ErrorViewModel;

namespace MissionPlanner.AvaloniaUI.App.Views.ConfigTuning.Tabs;

/// <summary>Provides the searchable full parameter list through the shared safe editing session.</summary>
public partial class FullParametersListTabViewModel : ParametersViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IDialogService dialogService;
    private readonly IDomainFactory domainFactory;
    private readonly ParametersFileHandler parametersFileHandler;
    private readonly IUserConfirmationService confirmation;
    private readonly IParameterProfileRepository profiles;
    private readonly IParameterProfileService profileWorkflow;
    private ParameterApplyReport? lastApplyReport;
    private bool disposed;
    private int sessionRefreshScheduled;
    private readonly IUserNotificationService userNotificationService;


    /// <summary>Initializes the Full Parameters List tab.</summary>
    /// <param name="connectionSession">The current connection-scoped services.</param>
    /// <param name="activeVehicle">The application active-vehicle context.</param>
    /// <param name="editSessionFactory">The shared parameter editing-session factory.</param>
    /// <param name="dialogService">The extended dialog service.</param>
    /// <param name="domainFactory">The domain view factory.</param>
    /// <param name="parametersFileHandler">The parameter import/export adapter.</param>
    /// <param name="confirmation">The hazardous-action confirmation service.</param>
    /// <param name="profiles">The named profile repository.</param>
    /// <param name="profileWorkflow">The profile compatibility and staging workflow.</param>
    /// <param name="parameterLoadStatus"></param>
    /// <param name="domainEventHub"></param>
    /// <param name="userNotificationService"></param>
    /// <param name="logger">The logger.</param>
    public FullParametersListTabViewModel(
        IVehicleConnectionSession connectionSession,
        IActiveVehicleContext activeVehicle,
        IParameterEditSessionFactory editSessionFactory, IDialogService dialogService,
        IDomainFactory domainFactory,
        ParametersFileHandler parametersFileHandler,
        IUserConfirmationService confirmation,
        IParameterProfileRepository profiles,
        IParameterProfileService profileWorkflow,
        IVehicleParameterLoadStatusContext parameterLoadStatus,
        IDomainEventHub domainEventHub,
        IUserNotificationService userNotificationService,
        ILogger<FullParametersListTabViewModel> logger)
        : base(connectionSession, activeVehicle, editSessionFactory, dialogService, domainFactory, parameterLoadStatus, domainEventHub, logger)
    {
        this.activeVehicle = activeVehicle;
        this.userNotificationService = userNotificationService;
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


    [RelayCommand]
    private async Task LoadFromEditorAsync(CancellationToken cancellationToken)
    {
        if (EditSession is null)
        {
            SetMessages(errorMessage: "Refresh vehicle parameters before importing parameters.");
            return;
        }

        try
        {
            var viewModel = domainFactory.Create<ParametersEditorViewModel, Action<ParametersEditorViewModel>>(vm =>
            {
                var fullList = EditSession.Fields.Select(ToVehicleParameter).ToList();
                var parameters = vm.UpdateParameters(fullList);
                using var notifications = EditSession.DeferChangeNotifications();
                foreach (var parameter in parameters)
                {
                    EditSession.TrySetPending(parameter.Name, parameter.Value, out var _);
                }

                SetMessages($"Imported {parameters.Count} matching values as unapplied edits.");
                HasRows = Parameters.Count > 0;
            });

            //TODO: ParametersEditorView
            throw new NotImplementedException();
            //    var pageView = domainFactory.Create<ParametersEditorView, ParametersEditorViewModel>(viewModel);

            //    await dialogService.ShowAsync(pageView, true, cancellationToken);
        }
        catch (Exception exception)
        {
            var viewModel = domainFactory.Create<ErrorViewModel, string>(exception.Message + "\nEnsure there is a connection and try again");
            var options = AvaloniaDialogService.CreateDialogOptions("Load failed", "Ok", null);
            var result = await dialogService.ShowOverlayDialogAsync<ErrorView, ErrorViewModel>(viewModel, options, cancellationToken: cancellationToken);
        }

        HasRows = Parameters.Count > 0;
    }


    [RelayCommand]
    private async Task LoadFromFileAsync(CancellationToken cancellationToken)
    {
        if (EditSession is null)
        {
            SetMessages(errorMessage: "Refresh vehicle parameters before importing a parameter file.");
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
        }
        catch (Exception exception)
        {
            var viewModel = domainFactory.Create<ErrorViewModel, string>(exception.Message + "\nEnsure there is a connection and try again");
            var options = AvaloniaDialogService.CreateDialogOptions("Load from file failed", "Ok", null);
            var result = await dialogService.ShowOverlayDialogAsync<ErrorView, ErrorViewModel>(viewModel, options, cancellationToken: cancellationToken);
        }

        HasRows = Parameters.Count > 0;
    }

    [RelayCommand]
    private async Task LoadFromJsonFileAsync(CancellationToken cancellationToken)
    {
        if (EditSession is null)
        {
            SetMessages(errorMessage: "Refresh vehicle parameters before importing a parameter file.");
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
        }
        catch (Exception exception)
        {
            var viewModel = domainFactory.Create<ErrorViewModel, string>(exception.Message + "\nEnsure there is a connection and try again");
            var options = AvaloniaDialogService.CreateDialogOptions("Load from Json file failed", "Ok", null);
            var result = await dialogService.ShowOverlayDialogAsync<ErrorView, ErrorViewModel>(viewModel, options, cancellationToken: cancellationToken);
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
                await userNotificationService.NotifyAsync(
                    new UserNotification($"File saved to:\n{result}", VehicleId: activeVehicle.VehicleId), cancellationToken);
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
                await userNotificationService.NotifyAsync(
                    new UserNotification($"File saved to:\n{result}", VehicleId: activeVehicle.VehicleId), cancellationToken);
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
                return;
            }
            var accepted = await confirmation.ConfirmAsync(
                "Review parameter writes",
                $"{preview}{skippedPreview}{Environment.NewLine}{Environment.NewLine}{rebootCount} change(s) require reboot.",
                $"Write {plan.Entries.Count} parameters",
                connectionCancellation.Token);
            if (!accepted)
            {
                Logger.LogInformation("Parameter write plan was cancelled for {VehicleId}.", EditSession.VehicleId);
                SetMessages("Parameter write cancelled. No values were sent.");
                return;
            }

            SetBusy();
            SetMessages($"Applying {plan.Entries.Count} modified parameters...");
            var progress = new Progress<ParameterApplyProgress>(value =>
                Dispatcher.Dispatch(() => ProgressMessage = $"{value.Index}/{value.Total}: {value.Name} — {value.Message}"));


            var report = await EditSession.ApplyAsync(plan, progress, connectionCancellation.Token);

            lastApplyReport = report;
            RebootRequired |= report.RebootRequired;
            var statusMessage = report.Success ? $"Confirmed {report.Confirmed.Count} parameter changes by vehicle readback." : null;
            var errorMessage = report.Success ? null : BuildResultSummary(report);

            Dispatcher.Dispatch(() => SetMessages(statusMessage, errorMessage));
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

        //TODO: ParameterComparisonView
        throw new NotImplementedException();


        //var pageView = domainFactory.Create<ParameterComparisonView, ParameterComparisonViewModel>(viewModel);
        //await dialogService.ShowAsync(pageView, new DialogOptions());
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
            }

            return;
        }
        await ShowMessageAsync("Parameter profiles", saved.Count == 0
            ? "No named parameter profiles have been saved."
            : string.Join(Environment.NewLine, saved.Select(profile => $"{profile.Name} — {profile.Values.Count} values — {profile.UpdatedAt:g}")),
            cancellationToken);
    }

    private async Task<bool> ShowMessageAsync(string title, string message, CancellationToken cancellationToken)
    {
        var options = AvaloniaDialogService.CreateDialogOptions(title, "Ok", null);
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
            var retry = await EditSession.RetryFailedAsync(lastApplyReport, cancellationToken: cancellationToken);
            lastApplyReport = retry;
            RebootRequired |= retry.RebootRequired;
            SetMessages(retry.Success ? $"Confirmed {retry.Confirmed.Count} retried changes." : null, retry.Success ? null : BuildResultSummary(retry));
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
        return HasConnection && HasRows && EditSession is { IsDirty: true, IsValid: true };
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
        return HasConnection && !IsBusy && EditSession is { IsDirty: true, IsValid: true };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(IsBusy) or nameof(HasConnection) or nameof(HasParameters) or nameof(HasRows)))
        {
            return;
        }
        WriteParametersCommand.NotifyCanExecuteChanged();
        CompareParametersCommand.NotifyCanExecuteChanged();
        RevertChangesCommand.NotifyCanExecuteChanged();
        SaveToFileCommand.NotifyCanExecuteChanged();
        SaveToJsonFileCommand.NotifyCanExecuteChanged();
    }

    /// <inheritdoc />
    protected override void OnEditSessionChanged()
    {
        base.OnEditSessionChanged();
        UpdateEditSessionCommandState();
    }

    /// <inheritdoc />
    protected override void OnEditSessionFieldChanged(string? fieldName)
    {
        base.OnEditSessionFieldChanged(fieldName);
        UpdateEditSessionCommandState();
    }

    private void UpdateEditSessionCommandState()
    {
        WriteParametersCommand.NotifyCanExecuteChanged();
        // RetryFailedCommand.NotifyCanExecuteChanged();
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
