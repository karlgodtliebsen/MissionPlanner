using Avalonia.Controls;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public interface INavigationService
{
    event Action<Page>? CurrentPageChanged;

    Task NavigateAsync(string route);

    Task PushAsync(Page page);

    Task GoBackAsync();
}
