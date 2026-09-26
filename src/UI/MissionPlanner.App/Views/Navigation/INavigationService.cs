using Avalonia.Controls;

namespace MissionPlanner.App.Views.Navigation;

public interface INavigationService
{
    event Action<Page>? CurrentPageChanged;

    /// <summary>The displayed route, including an explicitly requested section.</summary>
    string? CurrentRoute { get; }

    Task NavigateAsync(string route);

    Task PushAsync(Page page);

    Task GoBackAsync();
}
