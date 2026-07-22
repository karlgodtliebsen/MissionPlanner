using CommunityToolkit.Maui.Alerts;
using MissionPlanner.Core.Notifications;

namespace MissionPlanner.App.Presentation;

/// <summary>
/// Presents framework-neutral user notifications using the current MAUI window.
/// </summary>
public sealed class MauiUserNotificationService : IUserNotificationService
{
    private readonly IDispatcher dispatcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="MauiUserNotificationService"/> class.
    /// </summary>
    /// <param name="dispatcher">The UI dispatcher.</param>
    public MauiUserNotificationService(IDispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken = default) =>
        dispatcher.DispatchAsync(async () =>
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
