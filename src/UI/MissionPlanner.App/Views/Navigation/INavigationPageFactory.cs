using Avalonia.Controls;

namespace MissionPlanner.App.Views.Navigation;

public interface INavigationPageFactory
{
    Page Create(string route);
}