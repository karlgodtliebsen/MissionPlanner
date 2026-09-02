using Avalonia.Controls;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public interface INavigationService
{
    void Attach(NavigationPage navigationPage, DrawerPage drawerPage);

    Task NavigateAsync(string route);

    Task PushAsync(Page page);

    Task GoBackAsync();
}
