using Avalonia.Controls;

namespace MissionPlanner.App.Views.Logs;

/// <summary>Activates a logging section through its typed DI key.</summary>
public interface ILogsViewFactory
{
    /// <summary>Creates a new view for a known section.</summary>
    Control Create(LogsSection section);
}