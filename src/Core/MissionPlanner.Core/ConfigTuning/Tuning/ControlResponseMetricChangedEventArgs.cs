namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Provides the event payload for a read-only control-response update.</summary>
/// <param name="metric">The latest metric.</param>
public sealed class ControlResponseMetricChangedEventArgs(ControlResponseMetric metric) : EventArgs
{
    /// <summary>Gets the latest metric.</summary>
    public ControlResponseMetric Metric { get; } = metric;
}
