using Avalonia.Controls;
using MissionPlanner.AvaloniaUI.App.Utilities.Dispatching;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public sealed class AvaloniaNavigationService
    : INavigationService
{
    private readonly INavigationPageFactory pageFactory;
    private readonly IUiDispatcher dispatcher;

    private readonly SemaphoreSlim navigationGate = new(1, 1);

    private NavigationPage? navigationPage;
    private DrawerPage? drawerPage;

    private string? currentRoute;

    public AvaloniaNavigationService(
        INavigationPageFactory pageFactory,
        IUiDispatcher dispatcher)
    {
        this.pageFactory = pageFactory;
        this.dispatcher = dispatcher;
    }

    public void Attach(
        NavigationPage navigationPage,
        DrawerPage drawerPage)
    {
        this.navigationPage = navigationPage;
        this.drawerPage = drawerPage;
        currentRoute = null;
    }

    public async Task NavigateAsync(string route)
    {
        if (route == currentRoute)
        {
            if (drawerPage is not null)
            {
                drawerPage.IsOpen = false;
            }
            return;
        }

        await navigationGate.WaitAsync();

        try
        {
            await dispatcher.DispatchAsync(async () =>
            {
                var navigation = GetNavigationPage();

                //
                // Main menu navigation represents changing
                // application section, not drilling deeper.
                //
                if (navigation.StackDepth > 1)
                {
                    await navigation.PopToRootAsync();
                }

                var page = pageFactory.Create(route);

                await navigation.ReplaceAsync(page);

                currentRoute = route;

                drawerPage?.IsOpen = false;
            });
        }
        finally
        {
            navigationGate.Release();
        }
    }

    public async Task PushAsync(Page page)
    {
        await dispatcher.DispatchAsync(async () => await GetNavigationPage()
            .PushAsync(page));
    }

    public async Task GoBackAsync()
    {
        await dispatcher.DispatchAsync(async () =>
        {
            var navigation = GetNavigationPage();

            if (navigation.CanGoBack)
            {
                await navigation.PopAsync();
            }
        });
    }

    private NavigationPage GetNavigationPage()
    {
        return navigationPage
               ?? throw new InvalidOperationException(
                   "NavigationPage has not been attached.");
    }
}
