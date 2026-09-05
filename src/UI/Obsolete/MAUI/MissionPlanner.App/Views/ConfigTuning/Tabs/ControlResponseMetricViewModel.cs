namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Projects one read-only live control-response metric.</summary>
/// <param name="Axis">The protocol axis label.</param>
/// <param name="Desired">The desired response.</param>
/// <param name="Achieved">The achieved response.</param>
/// <param name="Error">The response error.</param>
/// <param name="Contributions">The controller contribution summary.</param>
public sealed record ControlResponseMetricViewModel(
    string Axis,
    float Desired,
    float Achieved,
    float Error,
    string Contributions);
