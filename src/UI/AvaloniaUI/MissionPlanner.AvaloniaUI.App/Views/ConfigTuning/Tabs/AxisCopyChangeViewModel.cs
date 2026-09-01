namespace MissionPlanner.AvaloniaUI.App.Views.ConfigTuning.Tabs;

/// <summary>Projects one proposed axis-copy change for explicit review.</summary>
/// <param name="Component">The copied component.</param>
/// <param name="SourceParameter">The source parameter.</param>
/// <param name="TargetParameter">The target parameter.</param>
/// <param name="BeforeValue">The current target pending value.</param>
/// <param name="AfterValue">The proposed source value.</param>
public sealed record AxisCopyChangeViewModel(
    string Component,
    string SourceParameter,
    string TargetParameter,
    double BeforeValue,
    double AfterValue);

