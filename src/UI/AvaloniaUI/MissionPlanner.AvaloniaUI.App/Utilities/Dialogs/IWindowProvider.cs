using Avalonia.Controls;

namespace MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;

public interface IWindowProvider
{
    Window? ActiveWindow
    {
        get;
    }

    void SetMainWindow(Window window);
}
