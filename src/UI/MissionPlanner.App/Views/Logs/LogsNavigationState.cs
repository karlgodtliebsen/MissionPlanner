namespace MissionPlanner.App.Views.Logs;

/// <summary>Remembers the chosen logs section for this application session.</summary>
public sealed class LogsNavigationState
{
    /// <summary>Remembers the selected logging section.</summary>
    public LogsSection SelectedSection
    {
        get; set;
    }
}
