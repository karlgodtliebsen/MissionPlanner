using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Models;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dialogs.SubViews;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Comparison;
using MissionPlanner.Core.ConfigTuning.Profiles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Parameters;
using Ursa.Controls;

namespace MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;

/// <summary>Provides the searchable full parameter list through the shared safe editing session.</summary>
public partial class ParametersEditorTabViewModel : ParametersViewModel
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

    /// <summary>Gets or sets the selected Editor, Quick Editor, or comparison tab index.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMainEditorSelected))]
    [NotifyPropertyChangedFor(nameof(IsQuickEditorSelected))]
    [NotifyPropertyChangedFor(nameof(IsComparisonSelected))]
    [NotifyPropertyChangedFor(nameof(IsProfileSelected))]
    [NotifyPropertyChangedFor(nameof(IsEditorExpanded))]
    [NotifyPropertyChangedFor(nameof(EditorColumnMaxWidth))]
    [NotifyPropertyChangedFor(nameof(ToggleEditorWidthText))]
    public partial int SelectedTabIndex
    {
        get; set;
    }

    /// <summary>Gets or sets whether the main editor uses the available page width.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditorExpanded))]
    [NotifyPropertyChangedFor(nameof(EditorColumnMaxWidth))]
    [NotifyPropertyChangedFor(nameof(ToggleEditorWidthText))]
    public partial bool IsMainEditorExpanded
    {
        get; set;
    }

    /// <summary>Gets whether the selected tab is displaying the expanded main editor.</summary>
    public bool IsEditorExpanded => IsMainEditorSelected && IsMainEditorExpanded;

    /// <summary>Gets the column width limit, retaining the compact layout for other tabs.</summary>
    public double EditorColumnMaxWidth => IsEditorExpanded ? double.PositiveInfinity : 900;

    /// <summary>Gets the accessible label for the editor width toggle.</summary>
    public string ToggleEditorWidthText => IsEditorExpanded ? "Restore compact editor width" : "Expand editor to full width";

    [RelayCommand]
    private void ToggleEditorWidth()
    {
        IsMainEditorExpanded = !IsMainEditorExpanded;
    }

    /// <summary>Gets whether the full parameter editor's toolbar actions are visible.</summary>
    public bool IsMainEditorSelected => SelectedTabIndex == 0;

    /// <summary>Gets whether the text editor's toolbar actions are visible.</summary>
    public bool IsQuickEditorSelected => SelectedTabIndex == 1;

    /// <summary>Gets whether the comparison toolbar actions are visible.</summary>
    public bool IsComparisonSelected => SelectedTabIndex == 3;

    public bool IsProfileSelected => SelectedTabIndex == 2;

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (IsQuickEditorSelected)
        {
            RefreshQuickEditor();
        }
        if (IsComparisonSelected)
        {
            RefreshComparison();
        }
        if (IsProfileSelected)
        {
            RefreshProfile();
        }
    }

    [RelayCommand]
    private void SelectQuickEditor()
    {
        SelectedTabIndex = 1;
    }

    [RelayCommand]
    private void SelectMainEditor()
    {
        SelectedTabIndex = 0;
    }

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
    /// <param name="comparisons">Compares the current session's live and pending values.</param>
    /// <param name="dateTimeProvider">Supplies timestamps for comparison exports.</param>
    public ParametersEditorTabViewModel(
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
        ILogger<ParametersEditorTabViewModel> logger,
        IVehicleConnectionService connections,
        IParameterComparisonService comparisons,
        IDateTimeProvider dateTimeProvider)
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
        this.comparisons = comparisons;
        this.dateTimeProvider = dateTimeProvider;
        SelectedItems.CollectionChanged += (_, _) => ApplyModifiedCommand.NotifyCanExecuteChanged();
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        if (disposed)
        {
            return;
        }
        pageActive = true;
        if (quickEditorLifetime.IsCancellationRequested)
        {
            quickEditorLifetime.Dispose();
            quickEditorLifetime = new CancellationTokenSource();
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
        pageActive = false;
        RefreshQuickEditor();
        quickEditorLifetime.Cancel();
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
        quickEditorLifetime.Cancel();
        quickEditorLifetime.Dispose();
        quickEditor?.Dispose();
        reconnectCancellation?.Cancel();
        base.Dispose();
        disposed = true;
    }

    /// <summary>Gets or sets whether the current parameter session has rows to display.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelLoadCommand))]
    [NotifyCanExecuteChangedFor(nameof(ParametersEditorTabViewModel.WriteParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ParametersEditorTabViewModel.CompareParametersCommand))]
    [NotifyCanExecuteChangedFor(nameof(ParametersEditorTabViewModel.RevertChangesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ParametersEditorTabViewModel.SaveToFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ParametersEditorTabViewModel.SaveToJsonFileCommand))]
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
    private Task UseQuickEditorAsync(CancellationToken cancellationToken)
    {
        return OpenQuickEditorAsync(null, cancellationToken);
    }

    /// <summary>Opens the quick editor using the current session and an optional detached presenter.</summary>
    public async Task OpenQuickEditorAsync(
        Func<ParametersEditorViewModel, CancellationToken, Task<ParametersEditorViewModel?>>? showEditor,
        CancellationToken cancellationToken)
    {
        if (EditSession is null)
        {
            SetMessages(errorMessage: "Refresh vehicle parameters before importing parameters.");
            NotificationManager?.Show(ErrorMessage ?? "");
            return;
        }
        try
        {
            using var viewModel = domainFactory.Create<ParametersEditorViewModel>();
            viewModel.DialogHostId = DialogHostId;
            var editorSession = EditSession;
            using var editorLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            void RefreshEditorParameters()
            {
                Dispatcher.Dispatch(() =>
                {
                    if (!editorLifetime.IsCancellationRequested)
                    {
                        viewModel.SetEditSession(editorSession,
                            ReferenceEquals(EditSession, editorSession) && HasConnection && !IsBusy);
                    }
                });
            }
            RefreshEditorParameters();
            viewModel.ConfigureApply(async token =>
            {
                using var applyLifetime = CancellationTokenSource.CreateLinkedTokenSource(token, editorLifetime.Token);
                applyLifetime.Token.ThrowIfCancellationRequested();
                return await ApplyQuickEditorChangesAsync(viewModel, editorSession, applyLifetime.Token);
            });
            var options = dialogService.CreateOptions("", null, null);
            options.FullScreen = true;
            options.CanDragMove = true;
            options.CanResize = true;
            options.Buttons = DialogButton.OKCancel;

            PropertyChangedEventHandler reportProgress = (_, args) =>
            {
                if (args.PropertyName is nameof(IsBusy) or nameof(HasConnection))
                {
                    RefreshEditorParameters();
                }
                if (!viewModel.ApplyModifiedCommand.IsRunning)
                {
                    return;
                }

                if (args.PropertyName == nameof(ProgressMessage))
                {
                    viewModel.WriteProgress = ProgressMessage;
                }

                if (args.PropertyName == nameof(StatusMessage))
                {
                    viewModel.StatusMessage = StatusMessage;
                }
            };
            PropertyChanged += reportProgress;
            editorSession.Changed += RefreshEditorParameters;
            try
            {
                _ = showEditor is null
                    ? await dialogService.ShowStandardAsync<ParametersEditorView, ParametersEditorViewModel>(viewModel, options, overLayHost: DialogHostId, cancellationToken: cancellationToken)
                    : await showEditor(viewModel, cancellationToken);
            }
            finally
            {
                PropertyChanged -= reportProgress;
                editorSession.Changed -= RefreshEditorParameters;
                editorLifetime.Cancel();
            }
        }
        catch (Exception exception)
        {
            var viewModel = domainFactory.Create<ErrorViewModel, string>(exception.Message + "\nEnsure there is a connection and try again");
            var options = dialogService.CreateOptions("Load failed", "Ok", null);
            await dialogService.ShowCustomDialogAsync<ErrorView, ErrorViewModel>(viewModel, options, overLayHost: DialogHostId, cancellationToken: cancellationToken);
        }

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
    private Task WriteParametersAsync(CancellationToken cancellationToken)
    {
        return ApplyParameterChangesAsync(cancellationToken);
    }

    private async Task ApplyParameterChangesAsync(CancellationToken cancellationToken, IReadOnlyList<string>? names = null)
    {
        var session = EditSession;
        if (session is null || !session.IsValid || !HasConnection || IsBusy)
        {
            return;
        }

        try
        {
            using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken);
            var reconnectTarget = connections.CaptureReconnectTarget();
            var plan = session.CreateWritePlan(names);
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
            var message = $"{preview}{skippedPreview}{Environment.NewLine}{Environment.NewLine}{rebootCount} change(s) require reboot.";
            var accepted = DialogHostId is null
                ? await confirmation.ConfirmAsync("Review parameter writes ", message, $" {plan.Entries.Count} parameters", connectionCancellation.Token)
                : await dialogService.ConfirmAsync(
                    dialogService.CreateOptions("Review parameter writes ", $" {plan.Entries.Count} parameters", null),
                    message, connectionCancellation.Token, overLayHost: DialogHostId);
            if (!accepted)
            {
                Logger.LogInformation("Parameter write plan was cancelled for {VehicleId}.", session.VehicleId);
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
                if (!ReferenceEquals(EditSession, session) || !session.IsValid)
                {
                    throw new InvalidOperationException("The vehicle changed while reviewing parameter writes. No values were written.");
                }

                var report = await session.ApplyAsync(plan, progress, connectionCancellation.Token);
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
    private Task CompareParametersAsync(CancellationToken cancellationToken)
    {
        SelectedTabIndex = 3;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task LoadPreSavedAsync(CancellationToken cancellationToken)
    {
        if (EditSession is not { IsValid: true } session)
        {
            SetMessages(null, "Load parameters for the current vehicle before opening profiles.");
            return;
        }
        var model = domainFactory.Create<ParameterProfilesViewModel, IParameterEditSession>(session);
        await model.InitializeAsync(cancellationToken);
        var options = dialogService.CreateOptions("Parameter profiles", "Close", null);
        options.FullScreen = true;
        await dialogService.ShowCustomDialogAsync<ParameterProfilesView, ParameterProfilesViewModel>(
            model, options, cancellationToken: cancellationToken);
    }
    /// <inheritdoc />
    protected override bool CanCancelLoad()
    {
        return IsBackgroundParameterLoadInProgress;
    }

    private async Task<bool> ShowMessageAsync(string title, string message, CancellationToken cancellationToken)
    {
        var options = dialogService.CreateOptions(title, "Ok", null);
        var result = await dialogService.ConfirmAsync(options, message, cancellationToken, overLayHost: DialogHostId);
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
        if (quickEditor?.ApplyModifiedCommand.IsRunning == true)
        {
            if (e.PropertyName == nameof(ProgressMessage))
            {
                quickEditor.WriteProgress = ProgressMessage;
            }
            if (e.PropertyName == nameof(StatusMessage))
            {
                quickEditor.StatusMessage = StatusMessage;
            }
        }
        if (e.PropertyName is not (nameof(IsBusy) or nameof(HasConnection) or nameof(HasParameters) or nameof(HasRows)))
        {
            return;
        }
        UpdateCommandState();
        RefreshQuickEditor();
        if (IsComparisonSelected)
        {
            RefreshComparison();
        }
    }

    /// <inheritdoc />
    protected override void OnEditSessionSynchronized()
    {
        base.OnEditSessionSynchronized();
        UpdateCommandState();
        RefreshQuickEditor();
        if (IsComparisonSelected)
        {
            RefreshComparison();
        }
    }

    private void UpdateCommandState()
    {
        WriteParametersCommand.NotifyCanExecuteChanged();
        CompareParametersCommand.NotifyCanExecuteChanged();
        RevertChangesCommand.NotifyCanExecuteChanged();
        SaveToFileCommand.NotifyCanExecuteChanged();
        SaveToJsonFileCommand.NotifyCanExecuteChanged();
        ApplyModifiedCommand.NotifyCanExecuteChanged();
        SelectAllWritableDifferencesCommand.NotifyCanExecuteChanged();
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
