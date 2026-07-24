namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Provides data for observable Planner settings changes.</summary>
public sealed class PlannerSettingsChangedEventArgs : EventArgs
{
    /// <summary>Initializes settings-change event data.</summary>
    /// <param name="previous">The previous settings.</param>
    /// <param name="current">The current settings.</param>
    /// <param name="restartRequiredSections">Changed sections that take effect after restart.</param>
    public PlannerSettingsChangedEventArgs(
        PlannerSettings previous,
        PlannerSettings current,
        IReadOnlyList<PlannerSettingsSection> restartRequiredSections)
    {
        Previous = previous;
        Current = current;
        RestartRequiredSections = restartRequiredSections;
    }

    /// <summary>Gets the previous settings.</summary>
    public PlannerSettings Previous { get; }

    /// <summary>Gets the current settings.</summary>
    public PlannerSettings Current { get; }

    /// <summary>Gets changed sections that take effect after restart.</summary>
    public IReadOnlyList<PlannerSettingsSection> RestartRequiredSections { get; }
}
