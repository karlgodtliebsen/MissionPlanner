namespace MissionPlanner.App.Views.Logs;

/// <summary>Remembers the chosen logs section for this application session.</summary>
public sealed class LogsNavigationState
{
    /// <summary>Telemetry is zero; application diagnostics is one.</summary>
    public int SelectedSection
    {
        get; set;
    }
}
