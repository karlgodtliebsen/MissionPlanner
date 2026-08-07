using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UraniumUI.Dialogs;
using UraniumUI.Infrastructure;
using UraniumUI.Resources;

namespace UraniumUI.Material.Dialogs;

/// <summary>
/// Provides extended dialog services.
/// </summary>
public class ExtendedDialogService : DefaultDialogService, IExtendedDialogService
{
    private readonly IDispatcher dispatcher;
    private readonly ILogger<ExtendedDialogService> logger;
    private readonly SemaphoreSlim navigationGate = new(1, 1);
    private readonly IServiceProvider serviceProvider;

    /// <inheritdoc />
    public ExtendedDialogService(IServiceProvider serviceProvider, IDispatcher dispatcher, IOptions<DialogOptions> options, ILogger<ExtendedDialogService> logger)
        : base(options)
    {
        this.dispatcher = dispatcher;
        this.logger = logger;
        this.serviceProvider = serviceProvider;
    }


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

    //private readonly SemaphoreSlim navigationGate = new(1, 1);
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

    /// <inheritdoc />
    public async Task DisplayViewExtendedAsync(string title, View content, string okText = "OK")
    {
        await DisplayLightweightViewAsync(title, content, okText, null);
    }

    /// <inheritdoc />
    public Task<bool> DisplayViewExtendedAsync(string title, View content, string okText, string cancelText)
    {
        return DisplayLightweightViewAsync(title, content, okText, cancelText);
    }

    private async Task<bool> DisplayLightweightViewAsync(
        string title,
        View content,
        string okText,
        string? cancelText)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Parent is not null)
        {
            throw new InvalidOperationException(
                "The dialog content already has a parent. " +
                "Create a new view for each dialog opening.");
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeRequested = 0;

        var popupPage = new DefaultDialogAnimatedContentPage { BackgroundColor = GetBackdropColor() };

        void RequestClose(bool accepted)
        {
            if (Interlocked.CompareExchange(
                    ref closeRequested,
                    1,
                    0) != 0)
            {
                return;
            }

            // Deliberately detached: the native button command and Windows
            // input event must finish before modal handler teardown starts.
            _ = Task.Run(() => CloseLightweightDialogSafelyAsync(popupPage, completion, accepted), CancellationToken.None);
        }

        var footerButtons = new Dictionary<string, Command> { [okText] = new(() => RequestClose(true)) };

        if (!string.IsNullOrWhiteSpace(cancelText))
        {
            footerButtons[cancelText] =
                new Command(() => RequestClose(false));
        }

        popupPage.Content = GetFrame(
            Page.Width,
            new VerticalStackLayout { Children = { GetHeader(title), content, GetDivider(), GetFooter(footerButtons) } });

        await dispatcher.DispatchAsync(async () => await Page.Navigation.PushModalAsync(
            ConfigurePopupPage(popupPage),
            false));

        return await completion.Task.ConfigureAwait(false);
    }

    private async Task CloseLightweightDialogSafelyAsync(DefaultDialogAnimatedContentPage popupPage, TaskCompletionSource<bool> completion, bool accepted)
    {
        await navigationGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        try
        {
            await dispatcher.DispatchAsync(async () =>
            {
                if (Page.Navigation.ModalStack.LastOrDefault() != popupPage)
                {
                    completion.TrySetResult(false);
                    return;
                }

                await popupPage.CloseAsync();
                completion.TrySetResult(accepted);
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to close the lightweight UraniumUI dialog.");
            completion.TrySetException(exception);
        }
        finally
        {
            navigationGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IDisposable> DisplayProgressCancellableAsync(string title, Func<string> message, string cancelText = "Cancel", CancellationTokenSource? tokenSource = default)
    {
        tokenSource ??= new CancellationTokenSource();

        var progress = new ActivityIndicator
        {
            IsRunning = true,
            IsVisible = true,
            HorizontalOptions = LayoutOptions.Center,
            Color = ColorResource.GetColor("Primary", "PrimaryDark", Colors.Blue),
            Margin = 20
        };
        var label = new Label { Text = message(), Margin = 20 };
        var verticalStackLayout = new VerticalStackLayout { Children = { GetHeader(title), label, progress } };

        if (!string.IsNullOrEmpty(cancelText))
        {
            verticalStackLayout.Children.Add(GetDivider());
            verticalStackLayout.Children.Add(GetFooter(new Dictionary<string, Command> { { cancelText, new Command(() => tokenSource?.Cancel()) } }));
        }

        var popupPage = new DefaultDialogAnimatedContentPage { BackgroundColor = GetBackdropColor(), Content = GetFrame(Page.Width, verticalStackLayout) };

        await dispatcher.DispatchAsync(async () => await Page.Navigation.PushModalAsync(ConfigurePopupPage(popupPage), false));

        var timer = dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100);
        timer.Tick += (s, e) => label.Text = message();

        var cancelAction = new DisposableAction(() =>
        {
            timer.Stop();
            dispatcher.DispatchAsync(() =>
            {
                if (Page.Navigation.ModalStack.LastOrDefault() == popupPage)
                {
                    Page.Navigation.PopModalAsync(false);
                }
            });
        });

        tokenSource.Token.Register(cancelAction.Dispose);
        timer.Start();
        return cancelAction;
    }

    private enum ViewDialogCloseReason
    {
        Accepted,
        Cancelled
    }

    private static double CalculateWidth(ViewDialogOptions options, double availableWidth) { return DeviceInfo.Current.Idiom == DeviceIdiom.Desktop ? Math.Min(options.DefaultDesktopWidth, availableWidth) : DeviceInfo.Current.Idiom == DeviceIdiom.Tablet ? availableWidth * options.DefaultTabletWidthRatio : DeviceInfo.Current.Idiom == DeviceIdiom.Phone ? availableWidth : availableWidth * 0.90; }
    private static double CalculateHeight(ViewDialogOptions options, double availableHeight) { return DeviceInfo.Current.Idiom == DeviceIdiom.Desktop ? Math.Min(options.DefaultDesktopHeight, availableHeight) : DeviceInfo.Current.Idiom == DeviceIdiom.Tablet ? availableHeight * options.DefaultTabletHeightRatio : DeviceInfo.Current.Idiom == DeviceIdiom.Phone ? availableHeight : availableHeight * 0.90; }

    private static Size ResolveSize(Page page, View content, ViewDialogOptions options)
    {
        var viewport = GetViewport(page);
        var margin = DeviceInfo.Current.Idiom == DeviceIdiom.Phone ? Math.Min(options.OuterMargin, 12) : options.OuterMargin;
        var availableWidth = Math.Max(1, viewport.Width - (margin * 2));
        var availableHeight = Math.Max(1, viewport.Height - (margin * 2));
        var defaultWidth = CalculateWidth(options, availableWidth);
        var defaultHeight = CalculateHeight(options, availableHeight);
        /*
         * Priority:
         * 1. Explicit ViewDialogOptions.RequestedSize
         * 2. WidthRequest/HeightRequest on the supplied ContentView
         * 3. Device-specific defaults
         */
        var requestedWidth = FirstPositive(options.RequestedSize?.Width ?? -1, content.WidthRequest, defaultWidth);
        var requestedHeight = FirstPositive(options.RequestedSize?.Height ?? -1, content.HeightRequest, defaultHeight);
        return new Size(Math.Clamp(requestedWidth, 1, availableWidth), Math.Clamp(requestedHeight, 1, availableHeight));
    }


    private static Size GetViewport(Page page)
    {
        if (page is { Width: > 0, Height: > 0 })
        {
            // The current application window/page is preferable on desktop,
            // // split-screen tablets, and resized windows.
            return new Size(page.Width, page.Height);
        }

        var display = DeviceDisplay.Current.MainDisplayInfo;
        return new Size(display.Width / display.Density, display.Height / display.Density);
    }

    private static double FirstPositive(params double[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (double.IsFinite(candidate) && candidate > 0)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No valid dialog dimension could be resolved.");
    }


    /// <inheritdoc />
    public async Task<bool> DisplayViewExtendedAsync(
        Page page,
        string title,
        View content,
        ViewDialogOptions? options = null,
        string okText = "OK",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(content);

        cancellationToken.ThrowIfCancellationRequested();

        if (content.Parent is not null)
        {
            throw new InvalidOperationException(
                "The dialog content already has a parent. " +
                "Create a new view instance or ensure that it was detached " +
                "from the previous dialog.");
        }

        options ??= new ViewDialogOptions();

        var size = ResolveSize(page, content, options);

        /*
         * The command does not close the popup itself.
         *
         * RunContinuationsAsynchronously is important: it prevents the service
         * continuation from running inline inside Command.Execute and therefore
         * avoids removing the button while its native Click event is executing.
         */
        var closeRequest =
            new TaskCompletionSource<ViewDialogCloseReason>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var popup = new Popup<bool>
        {
            WidthRequest = size.Width,
            HeightRequest = size.Height,
            Margin = 0,
            Padding = 0,
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            CanBeDismissedByTappingOutsideOfPopup = options.CanDismissByTappingOutside
        };

        var footer = GetFooter(
            new Dictionary<string, Command>
            {
                [okText] = new(() => closeRequest.TrySetResult(
                    ViewDialogCloseReason.Accepted))
            });

        ScrollView? contentScrollView = null;

        var presentedContent = content;

        if (options.WrapContentInScrollView)
        {
            contentScrollView = new ScrollView { Content = content };

            presentedContent = contentScrollView;
        }

        var contentHost = new ContentView { Content = presentedContent, Padding = new Thickness(20), HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };

        var dialogLayout = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) } };

        dialogLayout.Add(GetHeader(title), 0, 0);
        dialogLayout.Add(contentHost, 0, 1);
        dialogLayout.Add(GetDivider(), 0, 2);
        dialogLayout.Add(footer, 0, 3);

        var dialogFrame = new Border
        {
            Content = dialogLayout,
            WidthRequest = size.Width,
            HeightRequest = size.Height,
            Padding = 0,
            Margin = 0,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            StyleClass = ["SurfaceContainer", "Rounded"]
        };

        popup.Content = dialogFrame;

        var popupOptions = new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = options.CanDismissByTappingOutside, PageOverlayColor = DialogOptions.GetBackdropColor(), Shape = null, Shadow = null };

        /*
         * Do not pass cancellationToken here.
         *
         * CommunityToolkit documents that cancelling ShowPopupAsync only
         * cancels waiting for the result; it does not close the popup.
         * The service handles cancellation explicitly below.
         */
        var popupTask = page.ShowPopupAsync<bool>(popup, popupOptions, CancellationToken.None);

        using var cancellationRegistration = cancellationToken.Register(() => closeRequest.TrySetResult(ViewDialogCloseReason.Cancelled));

        var completedTask = await Task.WhenAny(popupTask, closeRequest.Task);

        /*
         * CommunityToolkit completed first. This normally means that the user
         * dismissed the popup by tapping outside it.
         */
        if (completedTask == popupTask)
        {
            var dismissedResult = await popupTask;

            return dismissedResult is
            {
                WasDismissedByTappingOutsideOfPopup: false,
                Result: true
            };
        }

        /*
         * The OK command or cancellation requested closure.
         *
         * Because closeRequest uses RunContinuationsAsynchronously, execution
         * reaches this code only after Command.Execute has returned.
         */
        var closeReason = await closeRequest.Task;

        await navigationGate.WaitAsync(cancellationToken);

        try
        {
            /*
             * This ThreadPool hop is intentional.
             *
             * It allows the current native Windows input event to return before
             * PopModalAsync starts removing and disconnecting the page's handlers.
             *
             * Task.Run(Func<Task>) returns a proxy task for the complete operation,
             * so exceptions and completion are propagated to the caller.
             */
            //Separate the OS Close native event call stack from this close functionality
            Task.Run(() => CloseDialog(popupTask, popup, popupOptions, contentScrollView, contentHost, closeReason), CancellationToken.None);
        }
        finally
        {
            navigationGate.Release();
        }

        /*
         * Wait until CommunityToolkit has completely removed the PopupPage and
         * raised PopupClosed. Only then is it safe to show another popup.
         */
        var result = await popupTask;

        return closeReason == ViewDialogCloseReason.Cancelled
            ? throw new OperationCanceledException(cancellationToken)
            : result is
            {
                WasDismissedByTappingOutsideOfPopup: false,
                Result: true
            };
    }

    private async Task CloseDialog(Task<IPopupResult<bool>> popupTask, Popup<bool> popup, PopupOptions popupOptions, ScrollView? contentScrollView, ContentView contentHost, ViewDialogCloseReason closeReason)
    {
        await dispatcher.DispatchAsync(async () =>
        {
            /*
             * An outside-tap closure could theoretically have completed
             * between Task.WhenAny and this dispatcher invocation.
             */
            if (popupTask.IsCompleted)
            {
                return;
            }

            /*
             * Prevent another outside-tap close from racing the explicit
             * close operation.
             */
            popup.CanBeDismissedByTappingOutsideOfPopup = false;
            popupOptions.CanBeDismissedByTappingOutsideOfPopup = false;

            /*
             * Detach the supplied view before the popup visual tree is
             * disconnected. This permits the same view instance to be
             * presented again and prevents it from remaining parented by
             * the old popup.
             */
            if (contentScrollView is not null)
            {
                contentScrollView.Content = null;
            }
            else
            {
                contentHost.Content = null;
            }

            /*
             * Closing must finish even when the caller's cancellation token
             * has already been cancelled. Otherwise an orphaned modal popup
             * can remain in the navigation stack.
             */
            await popup.CloseAsync(closeReason == ViewDialogCloseReason.Accepted, CancellationToken.None);
        });
    }
}
