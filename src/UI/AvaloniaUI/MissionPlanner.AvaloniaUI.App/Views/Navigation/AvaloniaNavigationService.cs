using Avalonia.Controls;
using MissionPlanner.AvaloniaUI.App.Utilities.Dispatching;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public sealed class AvaloniaNavigationService : INavigationService
{
    private readonly INavigationPageFactory pageFactory;
    private readonly IUiDispatcher dispatcher;
    private readonly SemaphoreSlim navigationGate = new(1, 1);
    private readonly List<NavigationEntry> navigationStack = [];
    private string? currentRoute;

    public AvaloniaNavigationService(INavigationPageFactory pageFactory, IUiDispatcher dispatcher)
    {
        this.pageFactory = pageFactory;
        this.dispatcher = dispatcher;
    }

    public event Action<Page>? CurrentPageChanged;

    public async Task NavigateAsync(string route)
    {
        await navigationGate.WaitAsync();
        try
        {
            if (route == currentRoute && navigationStack.Count == 1)
                return;

            await dispatcher.DispatchAsync(() =>
            {
                var page = pageFactory.Create(route);
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
                    return Task.CompletedTask;

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
