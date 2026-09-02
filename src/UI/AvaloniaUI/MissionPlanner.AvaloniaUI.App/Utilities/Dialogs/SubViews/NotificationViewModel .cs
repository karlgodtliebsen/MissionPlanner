using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Utilities.Dispatching;
using MissionPlanner.Core.Notifications;

namespace MissionPlanner.AvaloniaUI.App.Views.Common;

/// <summary>
/// ViewModel for the global status bar
/// </summary>
public partial class NotificationViewModel : ViewModelBase
{
    private readonly IUiDispatcher dispatcher;
    private readonly IApplicationNotificationStore notificationStore;

    private bool isDisposed;
    private CancellationTokenSource? bannerDismissal;
    private long bannerVersion;


    /// <summary>Gets the currently displayed transient application notification.</summary>
    [ObservableProperty] public partial string BannerMessage { get; private set; } = string.Empty;

    /// <summary>Gets whether a transient application notification is visible.</summary>
    [ObservableProperty]
    public partial bool HasBanner
    {
        get; private set;
    }

    /// <summary>Gets the severity of the currently displayed transient notification.</summary>
    [ObservableProperty]
    public partial UserNotificationSeverity BannerSeverity
    {
        get; private set;
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationViewModel"/> class.
    /// </summary>
    /// <param name="dispatcher">The Dispatcher for UI thread operations.</param>
    /// <param name="notificationStore">The application notification history and event source.</param>
    /// <param name="logger">The logger instance.</param>
    public NotificationViewModel(
        IUiDispatcher dispatcher,
        IApplicationNotificationStore notificationStore,
        ILogger<NotificationViewModel> logger)
    {
        this.dispatcher = dispatcher;
        this.notificationStore = notificationStore;

        // Subscribe to connection state changes
        notificationStore.NotificationAdded += OnNotificationAdded;
    }

    private void OnNotificationAdded(ApplicationNotificationAddedEventArgs args)
    {
        if (args.Notification.Presentation is UserNotificationPresentation.Dialog)
        {
            return;
        }

        dispatcher.Dispatch(() => ShowBanner(args.Notification));
    }

    private void ShowBanner(ApplicationNotificationEntry notification)
    {
        if (isDisposed)
        {
            return;
        }

        bannerDismissal?.Cancel();
        bannerDismissal?.Dispose();
        bannerDismissal = new CancellationTokenSource();
        var version = ++bannerVersion;

        BannerMessage = string.IsNullOrWhiteSpace(notification.Title)
            ? notification.Message
            : $"{notification.Title}: {notification.Message}";
        BannerSeverity = notification.Severity;
        HasBanner = true;

        var duration = notification.Presentation is UserNotificationPresentation.Banner
            ? TimeSpan.FromSeconds(8)
            : TimeSpan.FromSeconds(4);
        _ = DismissBannerAfterDelayAsync(version, duration, bannerDismissal.Token);
    }

    private async Task DismissBannerAfterDelayAsync(long version, TimeSpan duration, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
            await dispatcher.DispatchAsync(() =>
            {
                if (version == bannerVersion)
                {
                    DismissBanner();
                }
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    /// <summary>Dismisses the current transient notification.</summary>
    [RelayCommand]
    private void DismissBanner()
    {
        bannerVersion++;
        bannerDismissal?.Cancel();
        bannerDismissal?.Dispose();
        bannerDismissal = null;
        HasBanner = false;
        BannerMessage = string.Empty;
    }


    /// <inheritdoc />
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }
        isDisposed = true;
        notificationStore.NotificationAdded -= OnNotificationAdded;
        bannerDismissal?.Cancel();
        bannerDismissal?.Dispose();
        bannerDismissal = null;
    }
}
