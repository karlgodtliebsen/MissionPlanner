using MissionPlanner.Library.Logging;

namespace MissionPlanner.App.Presentation;

/// <summary>Opens a platform log directory without exposing filesystem APIs to a ViewModel.</summary>
public interface ILogFolderService
{
    /// <summary>Whether the platform supports opening a physical directory.</summary>
    bool IsSupported { get; }
    /// <summary>Opens the requested log area in the desktop file manager.</summary>
    Task OpenAsync(LogStorageArea area);
}