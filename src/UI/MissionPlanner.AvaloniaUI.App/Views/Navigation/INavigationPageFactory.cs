using Avalonia.Controls;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public interface INavigationPageFactory
{
    Page Create(string route);
}