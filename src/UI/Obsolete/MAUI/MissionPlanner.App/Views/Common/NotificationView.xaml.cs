using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.Common;

/// <summary>
/// A view that displays notifications to the user.
/// </summary>
public partial class NotificationView : ContentView
{
    /// <inheritdoc />
    public NotificationView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<NotificationViewModel>();
        BindingContext = viewModel;
    }
}

