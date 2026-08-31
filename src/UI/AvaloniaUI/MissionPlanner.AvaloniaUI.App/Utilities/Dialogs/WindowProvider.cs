using Avalonia.Controls;

namespace MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;

public sealed class WindowProvider : IWindowProvider
{
    private Window? mainWindow;

    public Window? ActiveWindow =>
        mainWindow;

    public void SetMainWindow(Window window)
    {
        mainWindow = window;
    }
}