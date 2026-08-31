using Avalonia.Controls;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public interface INavigationService
{
    Task NavigateAsync(string route);

    Task PushAsync(Page page);

    Task GoBackAsync();
}