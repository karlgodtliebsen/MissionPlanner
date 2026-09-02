using MissionPlanner.AvaloniaUI.App.Utilities;

namespace MissionPlanner.AvaloniaUI.App.Views.Common;

/// <inheritdoc />
public partial class TopBarView : UserControlViewBase<TopBarViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TopBarView"/> class with the specified view model.
    /// </summary>
    public TopBarView()
    {
        InitializeComponent();
    }

    ///// <inheritdoc/>
    //protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    //{
    //    base.OnAttachedToVisualTree(e);
    //    var topLevel = TopLevel.GetTopLevel(this);
    //    if (topLevel is null)
    //    {
    //        return;
    //    }

    //    ViewModel.NotificationManager = WindowNotificationManager.TryGetNotificationManager(topLevel, out var manager)
    //        ? manager
    //        : new WindowNotificationManager(topLevel);
    //}
}
