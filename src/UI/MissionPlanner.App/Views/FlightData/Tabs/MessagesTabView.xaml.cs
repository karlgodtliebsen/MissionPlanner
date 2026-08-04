using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class MessagesTabView : TabViewLifecycleContent<MessagesTabViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessagesTabView"/> class.
    /// </summary>
    public MessagesTabView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    public override void Activate()
    {
        base.Activate();
        ViewModel?.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <inheritdoc />
    public override void Deactivate()
    {
        base.Deactivate();
        ViewModel?.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MessagesTabViewModel.ScrollRequestVersion) &&
            ViewModel is { IsAutoScrollPaused: false } &&
            ViewModel.Items.LastOrDefault() is { } last)
        {
            MessageCollection.ScrollTo(last, position: ScrollToPosition.End, animate: true);
        }
    }
}
