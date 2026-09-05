using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.ConfigTuning.Comparison;
using MissionPlanner.Library.Math;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Selectable presentation row for a parameter comparison result.</summary>
public sealed partial class ParameterComparisonItemViewModel(ParameterComparisonRow row) : ObservableObject
{
    private ObservableCollection<ParameterComparisonItemViewModel>? selectedItems;

    /// <summary>Gets the underlying immutable comparison result.</summary>
    public ParameterComparisonRow Row { get; } = row;

    /// <summary>Gets or sets whether the safe difference is selected for staging.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Connects this row's selector to the owning view model's selected-items collection.</summary>
    public void TrackSelectionIn(ObservableCollection<ParameterComparisonItemViewModel> selection)
    {
        selectedItems = selection;
        SynchronizeSelection();
    }

    partial void OnIsSelectedChanged(bool value)
    {
        SynchronizeSelection();
    }

    /// <summary>Gets the parameter name.</summary>
    public string Name => Row.Name;

    /// <summary>Gets the display name.</summary>
    public string DisplayName => Row.DisplayName;

    /// <summary>Gets the left value.</summary>
    public double? LeftValue => Row.LeftValue;

    /// <summary>Gets the left value formatted at the parameter's declared precision.</summary>
    public string? LeftValueText => FormatValue(Row.LeftValue);

    /// <summary>Gets the right value.</summary>
    public double? RightValue => Row.RightValue;

    /// <summary>Gets the right value formatted at the parameter's declared precision.</summary>
    public string? RightValueText => FormatValue(Row.RightValue);

    /// <summary>Gets the numeric difference.</summary>
    public double? Difference => Row.Difference;

    /// <summary>Gets the difference formatted at the parameter's declared precision.</summary>
    public string? DifferenceText => FormatValue(Row.Difference);

    /// <summary>Gets the classification.</summary>
    public ParameterComparisonStatus Status => Row.Status;

    /// <summary>Gets the unit symbol.</summary>
    public string? Units => Row.Units;

    /// <summary>Gets whether the row is safe to stage.</summary>
    public bool CanStage => Row.CanStage;

    /// <summary>Gets the classification explanation.</summary>
    public string? Message => Row.Message;

    private void SynchronizeSelection()
    {
        if (selectedItems is null)
        {
            return;
        }

        if (IsSelected)
        {
            if (!selectedItems.Contains(this))
            {
                selectedItems.Add(this);
            }
        }
        else
        {
            selectedItems.Remove(this);
        }
    }

    private string? FormatValue(double? value)
    {
        if (value is null)
        {
            return null;
        }

        var increment = Row.Metadata?.Increment;
        return increment is > 0 && double.IsFinite(increment.Value)
            ? MathUtils.FormatAtStepPrecision(value.Value, increment)
            : value.Value.ToString("G7", System.Globalization.CultureInfo.CurrentCulture);
    }
}
