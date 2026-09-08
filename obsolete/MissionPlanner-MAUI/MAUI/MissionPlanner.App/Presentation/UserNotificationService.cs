using MissionPlanner.Core.Notifications;
using MissionPlanner.Library.DateTime.Domain;
using UraniumUI.Material.Dialogs;

namespace MissionPlanner.App.Presentation;

/// <summary>
/// Stores framework-neutral notifications and presents modal dialogs using the current window.
/// </summary>
public sealed class UserNotificationService : IUserNotificationService
{
    private readonly IExtendedDialogService dialogService;
    private readonly IApplicationNotificationStore notificationStore;
    private readonly IDateTimeProvider clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserNotificationService"/> class.
    /// </summary>
    /// <param name="dialogService"></param>
    /// <param name="notificationStore">The bounded local-notification history.</param>
    /// <param name="clock">The application clock.</param>
    public UserNotificationService(
        IExtendedDialogService dialogService,
        IApplicationNotificationStore notificationStore,
        IDateTimeProvider clock)
    {
        this.dialogService = dialogService;
        this.notificationStore = notificationStore;
        this.clock = clock;
    }

    /// <inheritdoc />
    public async Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken = default)
    {
        notificationStore.Add(notification, clock.UtcNow);

        if (notification.Presentation is not UserNotificationPresentation.Dialog)
        {
            if (notification.Presentation is UserNotificationPresentation.Toast or UserNotificationPresentation.Banner)
            {
                return;
            }

            throw new ArgumentOutOfRangeException(
                nameof(notification.Presentation),
                notification.Presentation,
                "Unsupported notification presentation.");
        }

        var window = Application.Current?.Windows.FirstOrDefault();
        var dispatcher = window?.Dispatcher;

        if (dispatcher is null)
        {
            return;
        }

        await dispatcher.DispatchAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await dialogService.ConfirmAsync(notification.Title ?? "Mission Planner", notification.Message, "OK", "Cancel");
        });
    }
}
