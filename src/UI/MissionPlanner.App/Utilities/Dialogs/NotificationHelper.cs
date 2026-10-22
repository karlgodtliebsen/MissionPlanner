using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using MissionPlanner.Library;
using Ursa.Controls;

namespace MissionPlanner.App.Utilities.Dialogs;

/// <summary>Attaches the notification and toast managers of a window to its view model.</summary>
public static class NotificationHelper
{
    /// <summary>Finds or creates the notification and toast managers of the window and assigns them to the view model.</summary>
    public static void SetupManagers(Control window, ViewModelBase? viewModel)
    {
        DomainException.ThrowIfNull(viewModel);
        var visualLayerManager = window.FindAncestorOfType<VisualLayerManager>();
        viewModel.NotificationManager =
            WindowNotificationManager.TryGetNotificationManager(visualLayerManager, out var notificationManager)
                ? notificationManager
                : new WindowNotificationManager(visualLayerManager) { MaxItems = 3 };
        viewModel.ToastManager = WindowToastManager.TryGetToastManager(visualLayerManager, out var toastManager)
            ? toastManager
            : new WindowToastManager(visualLayerManager) { MaxItems = 3 };
    }

}
