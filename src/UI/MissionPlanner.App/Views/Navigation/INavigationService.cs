using Avalonia.Controls;

namespace MissionPlanner.App.Views.Navigation;

/// <summary>Navigates between the pages of the application.</summary>
public interface INavigationService
{
    /// <summary>Occurs when the page shown by the shell changes.</summary>
    event Action<Page>? CurrentPageChanged;

    /// <summary>The displayed route, including an explicitly requested section.</summary>
    string? CurrentRoute { get; }

    /// <summary>Navigates to the page of the route.</summary>
    Task NavigateAsync(string route);

    /// <summary>Pushes a page onto the navigation stack.</summary>
    Task PushAsync(Page page);

    /// <summary>Goes back to the previous page.</summary>
    Task GoBackAsync();
}
