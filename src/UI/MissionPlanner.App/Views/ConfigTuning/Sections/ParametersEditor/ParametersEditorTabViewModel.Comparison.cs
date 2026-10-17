using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using MissionPlanner.Core.ConfigTuning.Comparison;
using MissionPlanner.Library.DateTime.Domain;

namespace MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;

public partial class ParametersEditorTabViewModel
{
    private readonly ObservableRangeCollection<ParameterComparisonItemViewModel> allRows = [];
    private ParameterComparisonResult? comparisonResult;
    private readonly IParameterComparisonService comparisons;
    private readonly IDateTimeProvider dateTimeProvider;

    /// <summary>Gets the currently filtered comparison rows.</summary>
    public ObservableRangeCollection<ParameterComparisonItemViewModel> Items { get; } = [];

    /// <summary>
    /// Gets the currently selected comparison rows.
    /// </summary>
    public ObservableCollection<ParameterComparisonItemViewModel> SelectedItems { get; } = [];

    /// <summary>Gets the available comparison status filters.</summary>
    public IReadOnlyList<string> Filters { get; } = ["Differences", "Missing", "Invalid", "Modified", "All"];

    /// <summary>Gets or sets the comparison status filter.</summary>
    [ObservableProperty]
    public partial string Filter { get; set; } = "Differences";

    partial void OnFilterChanged(string value)
    {
        FilterRows();
    }

    private void RefreshProfile()
    {

    }

    /// <summary>Compares the live and pending values in the supplied editing session.</summary>
    private void RefreshComparison()
    {
        if (EditSession is not { IsValid: true } editSession || !HasConnection)
        {
            comparisonResult = null;
            SelectedItems.Clear();
            allRows.Clear();
            FilterRows();
            return;
        }
        var now = dateTimeProvider.UtcNow;
        var firmware = editSession.Scope.FirmwareIdentity;
        var fields = editSession.Fields.ToDictionary(field => field.Name, StringComparer.Ordinal);
        var live = fields.Values.ToDictionary(
            field => field.Name,
            field => new ParameterComparisonInput(field.Name, field.LiveValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
            StringComparer.Ordinal);
        var pending = fields.Values.ToDictionary(
            field => field.Name,
            field => new ParameterComparisonInput(field.Name, field.PendingValue.ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
            StringComparer.Ordinal);
        var metadata = fields.Values.ToDictionary(field => field.Name, field => field.Metadata, StringComparer.Ordinal);

        comparisonResult = comparisons.Compare(
            new ParameterComparisonSource("Live", editSession.VehicleId.ToString(), now, firmware),
            live,
            new ParameterComparisonSource("Pending", editSession.VehicleId.ToString(), now, firmware),
            pending,
            metadata);

        var previous = allRows.ToDictionary(row => row.Name, StringComparer.Ordinal);
        SelectedItems.Clear();
        var rows = comparisonResult.Rows.Select(row =>
            new ParameterComparisonItemViewModel(row, fields[row.Name].ValidationError)).ToArray();
        foreach (var row in rows)
        {
            row.IsSelected = row.CanWrite && (!previous.TryGetValue(row.Name, out var old) ||
                old.RightValue != row.RightValue || old.IsSelected);
            row.TrackSelectionIn(SelectedItems);
        }

        allRows.ReplaceRange(rows);
        FilterRows();
    }
    [RelayCommand(CanExecute = nameof(CanCompareParameters))]
    private void SelectAllWritableDifferences()
    {
        foreach (var row in allRows)
        {
            row.IsSelected = row.CanWrite;
        }
    }

    [RelayCommand]
    private void ClearComparisonSelection()
    {
        foreach (var row in allRows)
        {
            row.IsSelected = false;
        }
    }

    private bool CanApplySelectedParameters()
    {
        return CanWriteParameters() && SelectedItems.Any(row => row.CanWrite);
    }

    [RelayCommand(CanExecute = nameof(CanApplySelectedParameters))]
    private async Task ApplyModifiedAsync(CancellationToken cancellationToken)
    {
        if (!CanApplySelectedParameters())
        {
            return;
        }
        var names = SelectedItems.Where(row => row.CanWrite).Select(row => row.Name).ToArray();
        await ApplyParameterChangesAsync(cancellationToken, names);
        RefreshComparison();
    }

    [RelayCommand]
    private async Task CopyComparisonAsync()
    {
        if (comparisonResult is not null)
        {
            await clipboard.SetTextAsync(comparisons.ExportCsv(comparisonResult));
            SetMessages($"Copied {comparisonResult.Rows.Count} comparison rows.");
        }
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

    private void FilterRows()
    {
        IEnumerable<ParameterComparisonItemViewModel> rows = allRows;
        rows = Filter switch
        {
            "Differences" => rows.Where(row => row.Status is not ParameterComparisonStatus.Equal),
            "Missing" => rows.Where(row => row.Status is ParameterComparisonStatus.OnlyOnLeft or ParameterComparisonStatus.OnlyOnRight or ParameterComparisonStatus.MetadataMissing),
            "Invalid" => rows.Where(row => row.Status is ParameterComparisonStatus.InvalidRightValue or ParameterComparisonStatus.ReadOnly || row.HasValidationError),
            "Modified" => rows.Where(row => row.Status is not ParameterComparisonStatus.Equal),
            var _ => rows
        };
        Items.ReplaceRange(rows);
    }
}
