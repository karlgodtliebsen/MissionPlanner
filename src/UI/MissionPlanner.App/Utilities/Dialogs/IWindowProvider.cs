using Avalonia.Controls;

namespace MissionPlanner.App.Utilities.Dialogs;

public interface IWindowProvider
{
    Window? ActiveWindow
    {
        get;
    }

    void SetMainWindow(Window window);
}
