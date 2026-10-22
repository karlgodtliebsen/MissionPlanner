using Avalonia.Controls;

namespace MissionPlanner.App.Utilities.Dialogs;

/// <summary>Provides the application windows.</summary>
public interface IWindowProvider
{
    /// <summary>Gets the window dialogs are shown from.</summary>
    Window? ActiveWindow
    {
        get;
    }

    /// <summary>Registers the main window.</summary>
    void SetMainWindow(Window window);
}
