using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using MissionPlanner.Library;
using Ursa.Controls;

namespace MissionPlanner.App.Utilities.Dialogs;

public static class NotificationHelper
{
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
