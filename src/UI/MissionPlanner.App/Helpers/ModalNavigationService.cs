using Microsoft.Extensions.Logging;

namespace MissionPlanner.App.Helpers;

/// <inheritdoc />
public sealed class ModalNavigationService(IServiceProvider serviceProvider, IDispatcher dispatcher, ILogger<ModalNavigationService> logger) : IModalNavigationService
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

    private readonly SemaphoreSlim navigationGate = new(1, 1);
    private int closeRequested;

    /// <summary>
    /// Requests closure of the current modal page.
    ///
    /// The returned task represents acceptance of the close request, not
    /// completion of the modal navigation operation.
    /// </summary>
    public Task CloseAsync(bool animated = true, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        /*
         * Ignore duplicate requests. A semaphore alone would serialize them,
         * potentially allowing a second request to pop another modal page.
         */
        if (Interlocked.CompareExchange(ref closeRequested, 1, 0) != 0)
        {
            return Task.CompletedTask;
        }

        /*
         * Deliberately do not return this task.
         *
         * The bound command must complete before the modal page is removed,
         * otherwise AsyncRelayCommand completion notifications can overlap
         * native handler teardown on Windows.
         */
        _ = Task.Run(() => CloseSafelyAsync(animated, cancellationToken), CancellationToken.None);

        return Task.CompletedTask;
    }

    private async Task CloseSafelyAsync(bool animated, CancellationToken cancellationToken)
    {
        try
        {
            await CloseCoreAsync(animated, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(
                "Modal close request was cancelled before navigation completed.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "An unexpected error occurred while closing the modal page.");
        }
        finally
        {
            Volatile.Write(ref closeRequested, 0);
        }
    }

    private async Task CloseCoreAsync(bool animated, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await navigationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await dispatcher.DispatchAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var navigation = GetNavigation();

                if (navigation.ModalStack.Count == 0)
                {
                    return;
                }

                await navigation.PopModalAsync(animated);
            }).ConfigureAwait(false);
        }
        finally
        {
            navigationGate.Release();
        }
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
