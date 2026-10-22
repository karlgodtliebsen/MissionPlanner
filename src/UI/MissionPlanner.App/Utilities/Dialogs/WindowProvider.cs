using Avalonia.Controls;

namespace MissionPlanner.App.Utilities.Dialogs;

/// <summary>Provides the main window of the application.</summary>
public sealed class WindowProvider : IWindowProvider
{
    private Window? mainWindow;

    /// <summary>Gets the main window, or null before it is registered.</summary>
    public Window? ActiveWindow =>
        mainWindow;

    /// <summary>Registers the main window.</summary>
    public void SetMainWindow(Window window)
    {
        mainWindow = window;
    }
}