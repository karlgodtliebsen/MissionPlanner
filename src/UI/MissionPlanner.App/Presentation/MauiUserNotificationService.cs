using CommunityToolkit.Maui.Alerts;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Library.DateTime.Domain;

namespace MissionPlanner.App.Presentation;

/// <summary>
/// Presents framework-neutral user notifications using the current MAUI window.
/// </summary>
public sealed class MauiUserNotificationService : IUserNotificationService
{
    private readonly IDispatcher dispatcher;
    private readonly IApplicationNotificationStore notificationStore;
    private readonly IDateTimeProvider clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MauiUserNotificationService"/> class.
    /// </summary>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="notificationStore">The bounded local-notification history.</param>
    /// <param name="clock">The application clock.</param>
    public MauiUserNotificationService(
        IDispatcher dispatcher,
        IApplicationNotificationStore notificationStore,
        IDateTimeProvider clock)
    {
        this.dispatcher = dispatcher;
        this.notificationStore = notificationStore;
        this.clock = clock;
    }

    /// <inheritdoc />
    public Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken = default)
    {
        notificationStore.Add(notification, clock.UtcNow);
        return dispatcher.DispatchAsync(async () =>
        {
            switch (notification.Presentation)
            {
                case UserNotificationPresentation.Toast:
                    await Toast.Make(notification.Message).Show(cancellationToken);
                    break;
                case UserNotificationPresentation.Banner:
                    await Snackbar.Make(notification.Message).Show(cancellationToken);
                    break;
                case UserNotificationPresentation.Dialog:
                    var page = Application.Current?.Windows.FirstOrDefault()?.Page;
                    if (page is not null)
                    {
                        await page.DisplayAlertAsync(notification.Title ?? "Mission Planner", notification.Message, "OK");
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(notification), notification.Presentation, "Unsupported notification presentation.");
            }
        });
    }
}
