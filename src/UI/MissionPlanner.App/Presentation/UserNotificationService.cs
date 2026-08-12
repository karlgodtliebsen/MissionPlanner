using CommunityToolkit.Maui.Alerts;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Library.DateTime.Domain;
using UraniumUI.Material.Dialogs;

namespace MissionPlanner.App.Presentation;

/// <summary>
/// Presents framework-neutral user notifications using the current window.
/// </summary>
public sealed class UserNotificationService : IUserNotificationService
{
    private readonly IDispatcher dispatcher;
    private readonly IExtendedDialogService dialogService;
    private readonly IApplicationNotificationStore notificationStore;
    private readonly IDateTimeProvider clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserNotificationService"/> class.
    /// </summary>
    /// <param name="dispatcher">The UI Dispatcher.</param>
    /// <param name="dialogService"></param>
    /// <param name="notificationStore">The bounded local-notification history.</param>
    /// <param name="clock">The application clock.</param>
    public UserNotificationService(
        IDispatcher dispatcher,
        IExtendedDialogService dialogService,
        IApplicationNotificationStore notificationStore,
        IDateTimeProvider clock)
    {
        this.dispatcher = dispatcher;
        this.dialogService = dialogService;
        this.notificationStore = notificationStore;
        this.clock = clock;
    }

    /// <inheritdoc />
    public async Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken = default)
    {
        notificationStore.Add(notification, clock.UtcNow);
        await dispatcher.DispatchAsync(async () =>
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
                    await dialogService.ConfirmAsync(notification.Title ?? "Mission Planner", notification.Message, "OK", "Cancel");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(notification), notification.Presentation, "Unsupported notification presentation.");
            }
        });
    }
}
