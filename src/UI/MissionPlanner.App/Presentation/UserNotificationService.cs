using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Library.DateTime.Domain;

namespace MissionPlanner.App.Presentation;

/// <summary>
/// Stores framework-neutral notifications and presents modal dialogs using the current window.
/// </summary>
public sealed class UserNotificationService : IUserNotificationService
{
    private readonly IUiDispatcher dispatcher;
    private readonly IDialogService dialogService;
    private readonly IApplicationNotificationStore notificationStore;
    private readonly IDateTimeProvider clock;
    private readonly ISpeechService? speech;
    private readonly IPlannerSettingsService? settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserNotificationService"/> class.
    /// </summary>
    /// <param name="dispatcher">The UI dispatcher instance.</param>
    /// <param name="dialogService"></param>
    /// <param name="notificationStore">The bounded local-notification history.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="speech">Optional local speech output.</param>
    /// <param name="settings">Saved speech preferences.</param>
    public UserNotificationService(
        IUiDispatcher dispatcher,
        IDialogService dialogService,
        IApplicationNotificationStore notificationStore,
        IDateTimeProvider clock,
        ISpeechService? speech = null,
        IPlannerSettingsService? settings = null)
    {
        this.dispatcher = dispatcher;
        this.dialogService = dialogService;
        this.notificationStore = notificationStore;
        this.clock = clock;
        this.speech = speech;
        this.settings = settings;
    }

    /// <inheritdoc />
    public async Task NotifyAsync(UserNotification notification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        notificationStore.Add(notification, clock.UtcNow);
        if (settings?.Current.Legacy is { SpeechEnabled: true } preferences && speech?.IsAvailable == true &&
            Enum.TryParse<UserNotificationSeverity>(preferences.SpeechSeverity, true, out var minimum) && notification.Severity >= minimum)
        {
            speech.TryEnqueue(notification.Message);
        }

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

        await dispatcher.DispatchAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var options = dialogService.CreateOptions(notification.Title ?? "Mission Planner");
            await dialogService.ConfirmAsync(options, notification.Message, cancellationToken);
        });
    }
}
