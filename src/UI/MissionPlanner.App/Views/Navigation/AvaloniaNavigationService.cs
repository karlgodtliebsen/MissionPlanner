using Avalonia.Controls;
using MissionPlanner.App.Utilities.Dispatching;

namespace MissionPlanner.App.Views.Navigation;

/// <summary>
/// Implements a navigation service for Avalonia applications, allowing navigation between pages and managing the navigation stack.
/// </summary>
public sealed class AvaloniaNavigationService : INavigationService
{
    private readonly INavigationPageFactory pageFactory;
    private readonly IUiDispatcher dispatcher;
    private readonly SemaphoreSlim navigationGate = new(1, 1);
    private readonly List<NavigationEntry> navigationStack = [];
    private string? currentRoute;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaNavigationService"/> class with the specified page factory and UI dispatcher. 
    /// </summary>
    /// <param name="pageFactory"></param>
    /// <param name="dispatcher"></param>
    public AvaloniaNavigationService(INavigationPageFactory pageFactory, IUiDispatcher dispatcher)
    {
        this.pageFactory = pageFactory;
        this.dispatcher = dispatcher;
    }

    public event Action<Page>? CurrentPageChanged;
    public string? CurrentRoute => currentRoute;

    public async Task NavigateAsync(string route)
    {
        await navigationGate.WaitAsync();
        try
        {
            var parts = route.Split('#', 2);
            var pageRoute = parts[0];
            var section = parts.Length == 2 ? parts[1] : null;
            if (section is null && route == currentRoute && navigationStack.Count == 1)
            {
                return;
            }

            await dispatcher.DispatchAsync(() =>
            {
                // Preserve local edits when switching sections on the current page.
                var page = section is not null && navigationStack.Count == 1 &&
                    currentRoute?.Split('#', 2)[0] == pageRoute
                    ? navigationStack[0].Page
                    : pageFactory.Create(pageRoute);
                if (section is not null)
                {
                    if (page is not ISectionNavigationPage sectionPage)
                        throw new InvalidOperationException($"Page '{pageRoute}' does not support section navigation.");
                    sectionPage.SelectSection(section);
                }
                navigationStack.Clear();
                navigationStack.Add(new NavigationEntry(route, page));
                currentRoute = route;
                CurrentPageChanged?.Invoke(page);
                return Task.CompletedTask;
            });
        }
        finally
        {
            navigationGate.Release();
        }
    }

    public async Task PushAsync(Page page)
    {
        await navigationGate.WaitAsync();
        try
        {
            await dispatcher.DispatchAsync(() =>
            {
                navigationStack.Add(new NavigationEntry(null, page));
                currentRoute = null;
                CurrentPageChanged?.Invoke(page);
                return Task.CompletedTask;
            });
        }
        finally
        {
            navigationGate.Release();
        }
    }

    public async Task GoBackAsync()
    {
        await navigationGate.WaitAsync();
        try
        {
            await dispatcher.DispatchAsync(() =>
            {
                if (navigationStack.Count <= 1)
                {
                    return Task.CompletedTask;
                }

                navigationStack.RemoveAt(navigationStack.Count - 1);
                var entry = navigationStack[^1];
                currentRoute = entry.Route;
                CurrentPageChanged?.Invoke(entry.Page);
                return Task.CompletedTask;
            });
        }
        finally
        {
            navigationGate.Release();
        }
    }

    private sealed record NavigationEntry(string? Route, Page Page);
}
