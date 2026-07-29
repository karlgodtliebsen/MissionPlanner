using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.ConfigTuning.Comparison;
using MissionPlanner.Library.Math;

namespace MissionPlanner.App.Views.ConfigTuning;

/// <summary>Selectable presentation row for a parameter comparison result.</summary>
public sealed partial class ParameterComparisonItemViewModel(ParameterComparisonRow row) : ObservableObject
{
    /// <summary>Gets the underlying immutable comparison result.</summary>
    public ParameterComparisonRow Row { get; } = row;

    /// <summary>Gets or sets whether the safe difference is selected for staging.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

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

    private string? FormatValue(double? value) =>
        value is null
            ? null
            : MathUtils.FormatAtStepPrecision(value.Value, Row.Metadata?.Increment);
}
