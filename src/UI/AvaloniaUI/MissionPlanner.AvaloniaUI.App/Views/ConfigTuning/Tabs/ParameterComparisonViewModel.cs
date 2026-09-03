using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using MissionPlanner.AvaloniaUI.App.Presentation;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Comparison;
using MissionPlanner.Library;
using MissionPlanner.Library.DateTime.Domain;
using Ursa.Controls;
using MissionPlanner.AvaloniaUI.App.Models;

namespace MissionPlanner.AvaloniaUI.App.Views.ConfigTuning.Tabs;

/// <summary>Provides the parameter comparison workspace.</summary>
public partial class ParameterComparisonViewModel : DialogViewModelBase
{
    private readonly ObservableRangeCollection<ParameterComparisonItemViewModel> allRows = [];
    private ParameterComparisonResult? comparisonResult;
    private readonly IParameterEditSession? editSession;
    private readonly IParameterComparisonService comparisons;
    private readonly ParametersFileHandler parametersFileHandler;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IDialogService dialogService;
    private readonly IUserConfirmationService confirmation;


    /// <summary>Provides the parameter comparison workspace.</summary>
    public ParameterComparisonViewModel(IParameterComparisonService comparisons, IDialogService dialogService, ParametersFileHandler parametersFileHandler, IDateTimeProvider dateTimeProvider, IUserConfirmationService confirmation, IParameterEditSession session)
    {
        this.comparisons = comparisons;
        this.dialogService = dialogService;
        this.parametersFileHandler = parametersFileHandler;
        this.dateTimeProvider = dateTimeProvider;
        this.confirmation = confirmation;
        editSession = session;
        Show();
    }

    [RelayCommand]
    private Task CloseAsync(CancellationToken cancellationToken)
    {
        return dialogService.CloseAsync(cancellationToken);
    }

    /// <summary>Gets the currently filtered comparison rows.</summary>
    public ObservableRangeCollection<ParameterComparisonItemViewModel> Items { get; } = [];

    /// <summary>
    /// Gets the currently selected comparison rows.
    /// </summary>
    public ObservableCollection<ParameterComparisonItemViewModel> SelectedItems { get; set; } = [];

    /// <summary>Gets the available comparison status filters.</summary>
    public IReadOnlyList<string> Filters { get; } = ["Differences", "Missing", "Invalid", "Modified", "All"];

    /// <summary>Gets or sets the comparison status filter.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = "Differences";

    partial void OnFilterChanged(string value)
    {
        FilterRows();
    }

    /// <summary>Compares the live and pending values in the supplied editing session.</summary>
    private void Show()
    {
        DomainException.ThrowIfNull(editSession);
        var now = dateTimeProvider.UtcNow;
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

        comparisonResult = comparisons.Compare(
            new ParameterComparisonSource("Live", editSession.VehicleId.ToString(), now, firmware),
            live,
            new ParameterComparisonSource("Pending", editSession.VehicleId.ToString(), now, firmware),
            pending,
            metadata);

        allRows.ReplaceRange(comparisonResult.Rows.Select(row => new ParameterComparisonItemViewModel(row)));
        FilterRows();
    }

    [RelayCommand]
    private void SelectAllSafeDifferences()
    {
        SelectedItems.Clear();
        foreach (var row in allRows)
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

        var selected = SelectedItems.Select(row => row.Name).ToArray();
        var staged = comparisons.Stage(comparisonResult, editSession, selected);
        Staged?.Invoke(this, staged.Count);
    }

    [RelayCommand]
    private async Task ApplyModifiedAsync(CancellationToken cancellationToken)
    {
        if (editSession is null)
        {
            return;
        }

        OverlayDialogOptions options;
        ParameterWritePlan plan;
        string message;
        try
        {
            plan = editSession.CreateWritePlan();
        }
        catch (InvalidOperationException exception)
        {
            options = AvaloniaDialogService.CreateDialogOptions("No changes to apply", "Ok", null);
            message = exception.Message;
            var result = await dialogService.ConfirmAsync(options, message, cancellationToken);
            return;
        }

        if (plan.Entries.Count == 0)
        {
            message = string.Join(Environment.NewLine, plan.Skipped.Select(item => $"{item.Name}: {item.Message}"));
            options = AvaloniaDialogService.CreateDialogOptions("No safe changes to apply", "Ok", null);
            var result = await dialogService.ConfirmAsync(options, message, cancellationToken);
            return;
        }

        var title = "Review parameter writes" + $"Write {plan.Entries.Count} parameters";
        message = $"Write {plan.Entries.Count} safe modified parameter(s)? " +
                     $"{plan.Skipped.Count} unsafe parameter(s) will be skipped. " +
                     $"{plan.RebootRequiredCount} confirmed change(s) will require reboot.";
        options = AvaloniaDialogService.CreateDialogOptions(title, "Ok", null);
        var accepted = await dialogService.ConfirmAsync(options, message, cancellationToken);

        if (!accepted)
        {
            return;
        }
        var report = await editSession.ApplyAsync(plan, cancellationToken: cancellationToken);
        message = string.Join(
            Environment.NewLine,
            report.Results
                .GroupBy(result => result.Outcome)
                .OrderBy(group => group.Key)
                .Select(group => $"{group.Key}: {group.Count()}"));

        title = report.Success ? "Parameters applied" : "Parameter apply report";
        options = AvaloniaDialogService.CreateDialogOptions(title, "Ok", null);
        await dialogService.ConfirmAsync(options, message, cancellationToken);
        Show();
    }

    [RelayCommand]
    private async Task ExportJsonAsync(CancellationToken cancellationToken)
    {
        if (comparisonResult is not null)
        {
            await parametersFileHandler.SaveTextFileAsync("parameter-comparison.json", comparisons.ExportJson(comparisonResult), cancellationToken);
        }
    }

    [RelayCommand]
    private async Task ExportCsvAsync(CancellationToken cancellationToken)
    {
        if (comparisonResult is not null)
        {
            await parametersFileHandler.SaveTextFileAsync("parameter-comparison.csv", comparisons.ExportCsv(comparisonResult), cancellationToken);
        }
    }

    /// <summary>Occurs after selected differences have been staged.</summary>
    public event EventHandler<int>? Staged;

    private void FilterRows()
    {
        IEnumerable<ParameterComparisonItemViewModel> rows = allRows;
        rows = Filter switch
        {
            "Differences" => rows.Where(row => row.Status is not ParameterComparisonStatus.Equal),
            "Missing" => rows.Where(row => row.Status is ParameterComparisonStatus.OnlyOnLeft or ParameterComparisonStatus.OnlyOnRight or ParameterComparisonStatus.MetadataMissing),
            "Invalid" => rows.Where(row => row.Status is ParameterComparisonStatus.InvalidRightValue or ParameterComparisonStatus.ReadOnly),
            "Modified" => rows.Where(row => row.CanStage),
            var _ => rows
        };
        Items.ReplaceRange(rows);
    }
}
