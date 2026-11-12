using Avalonia.Controls;

namespace MissionPlanner.App.Utilities.Dialogs;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

/// <summary>Provides the focused desktop window, falling back to the main window.</summary>
public sealed class WindowProvider : IWindowProvider
{
    private Window? mainWindow;

    /// <summary>Gets the focused window so confirmations stay with detached controls.</summary>
    public Window? ActiveWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?
            .Windows.FirstOrDefault(window => window.IsActive && !window.OwnedWindows.Any(child => child.IsActive)) ?? mainWindow;

    /// <summary>Registers the main window.</summary>
    public void SetMainWindow(Window window)
    {
        mainWindow = window;
    }
}
