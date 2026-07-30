namespace MissionPlanner.App.Helpers;

/// <inheritdoc />
public sealed class ModalNavigationService(IServiceProvider serviceProvider, IDispatcher dispatcher) : IModalNavigationService
{
    /// <inheritdoc />
    public Task ShowAsync<TPage>(bool animated = true, CancellationToken cancellationToken = default)
        where TPage : Page
    {
        cancellationToken.ThrowIfCancellationRequested();

        return dispatcher.DispatchAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = serviceProvider.GetRequiredService<TPage>();
            var navigation = GetNavigation();

            await navigation.PushModalAsync(page, animated);
        });
    }

    /// <inheritdoc />
    public Task ShowAsync(Page page, bool animated = true, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return dispatcher.DispatchAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var navigation = GetNavigation();
            await navigation.PushModalAsync(page, animated);
        });
    }

    /// <inheritdoc />
    public Task CloseAsync(bool animated = true, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return dispatcher.DispatchAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var navigation = GetNavigation();

            if (navigation.ModalStack.Count == 0)
            {
                return;
            }

            await navigation.PopModalAsync(animated);
        });
    }

    private static INavigation GetNavigation()
    {
        var application = Application.Current
                          ?? throw new InvalidOperationException(
                              "The MAUI application is not available.");

        var window = application.Windows.FirstOrDefault(candidate => candidate.Page is not null);

        var rootPage = window?.Page
                       ?? throw new InvalidOperationException(
                           "No active MAUI window with a root page was found.");

        return GetCurrentPage(rootPage).Navigation;
    }

    private static Page GetCurrentPage(Page page)
    {
        return page switch
        {
            Shell shell when shell.CurrentPage is not null => shell.CurrentPage,
            NavigationPage navigationPage => GetCurrentPage(navigationPage.CurrentPage),

            TabbedPage tabbedPage when tabbedPage.CurrentPage is not null => GetCurrentPage(tabbedPage.CurrentPage),

            FlyoutPage flyoutPage => GetCurrentPage(flyoutPage.Detail),

            var _ => page
        };
    }
}
