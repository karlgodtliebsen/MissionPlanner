using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using MissionPlanner.App.Helpers;
using MissionPlanner.App.Navigation;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Comparison;
using MissionPlanner.Library;
using MissionPlanner.Library.DateTime.Domain;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Provides the parameter comparison workspace.</summary>
public partial class ParameterComparisonViewModel : ObservableObject
{
    private readonly List<ParameterComparisonItemViewModel> allRows = [];
    private ParameterComparisonResult? comparisonResult;
    private readonly IParameterEditSession? editSession;
    private readonly IParameterComparisonService comparisons;
    private readonly ParametersFileHandler parametersFileHandler;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IModalNavigationService modalNavigationService;


    /// <summary>Provides the parameter comparison workspace.</summary>
    public ParameterComparisonViewModel(IParameterComparisonService comparisons, IModalNavigationService modalNavigationService, ParametersFileHandler parametersFileHandler, IDateTimeProvider dateTimeProvider, IParameterEditSession session)
    {
        this.comparisons = comparisons;
        this.modalNavigationService = modalNavigationService;
        this.parametersFileHandler = parametersFileHandler;
        this.dateTimeProvider = dateTimeProvider;
        editSession = session;
        Show();
    }

    [RelayCommand]
    private Task CloseAsync(CancellationToken cancellationToken)
    {
        return modalNavigationService.CloseAsync(true, cancellationToken);
    }

    /// <summary>Gets the currently filtered comparison rows.</summary>
    public ObservableRangeCollection<ParameterComparisonItemViewModel> Rows { get; } = [];

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

        allRows.Clear();
        allRows.AddRange(comparisonResult.Rows.Select(row => new ParameterComparisonItemViewModel(row)));
        FilterRows();
    }

    [RelayCommand]
    private void SelectAllSafeDifferences()
    {
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

        var selected = allRows.Where(row => row.IsSelected).Select(row => row.Name).ToArray();
        var staged = comparisons.Stage(comparisonResult, editSession, selected);
        Staged?.Invoke(this, staged.Count);
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
        Rows.ReplaceRange(rows);
    }
}
