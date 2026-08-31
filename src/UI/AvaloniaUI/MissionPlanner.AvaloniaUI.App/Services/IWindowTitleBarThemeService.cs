namespace MissionPlanner.AvaloniaUI.App.Services;

/// <summary>
/// Applies semantic application colors to a platform window title bar.
/// </summary>
public interface IWindowTitleBarThemeService
{
    /// <summary>Connects the platform title bar to the active application theme.</summary>
    /// <param name="window">The MAUI window.</param>
    /// <param name="themeManager">The application theme manager.</param>
    /// <param name="activeResources">The active semantic color dictionary.</param>
    //void Attach(Window window, IThemeManager themeManager, ResourceDictionary activeResources);
}
