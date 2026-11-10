using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.ConfigTuning.Comparison;
using MissionPlanner.Core.ConfigTuning;
using System.Globalization;

namespace MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;

/// <summary>Selectable presentation row for a parameter comparison result.</summary>
public sealed partial class ParameterComparisonItemViewModel(ParameterComparisonRow row, string? validationError = null) : ObservableObject
{
    private ObservableCollection<ParameterComparisonItemViewModel>? selectedItems;

    /// <summary>Gets the underlying immutable comparison result.</summary>
    public ParameterComparisonRow Row { get; } = row;

    /// <summary>Gets or sets whether the difference is selected for the owner's next operation.</summary>
    [ObservableProperty]
    public partial bool IsSelected
    {
        get; set;
    }

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

    /// <summary>Gets the left value without rounding to the suggested editor step.</summary>
    public string? LeftValueText => FormatValue(Row.LeftValue);

    /// <summary>Gets the right value.</summary>
    public double? RightValue => Row.RightValue;

    /// <summary>Gets the right value without rounding to the suggested editor step.</summary>
    public string? RightValueText => FormatValue(Row.RightValue);

    /// <summary>Gets the numeric difference.</summary>
    public double? Difference => Row.Difference;

    /// <summary>Gets the difference between the displayed values.</summary>
    public string? DifferenceText => Row.Status == ParameterComparisonStatus.Equal ? "0" :
        Row.LeftValue is { } left && Row.RightValue is { } right
            ? ParameterValueText.FormatDifference(left, right, CultureInfo.CurrentCulture) : null;

    /// <summary>Gets the classification.</summary>
    public ParameterComparisonStatus Status => Row.Status;

    /// <summary>Gets the unit symbol.</summary>
    public string? Units => Row.Units;

    /// <summary>Gets whether the row is safe to stage.</summary>
    public bool CanStage => Row.CanStage;

    /// <summary>Gets whether this pending difference passes session validation for selection.</summary>
    public bool CanWrite => Row.CanStage && !HasValidationError;

    /// <summary>Gets whether the shared session rejected the pending value.</summary>
    public bool HasValidationError => validationError is not null;

    /// <summary>Gets the classification explanation.</summary>
    public string? Message => validationError ?? Row.Message;

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

        return ParameterValueText.Format(value.Value, CultureInfo.CurrentCulture);
    }
}
