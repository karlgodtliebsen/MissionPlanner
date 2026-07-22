using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class MessagesTabView : ContentView
{
    private MessagesTabViewModel? viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagesTabView"/> class.
    /// </summary>
    public MessagesTabView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<MessagesTabViewModel>();
        BindingContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MessagesTabViewModel.ScrollRequestVersion) &&
            viewModel is { IsAutoScrollPaused: false } &&
            viewModel.Items.LastOrDefault() is { } last)
        {
            MessageCollection.ScrollTo(last, position: ScrollToPosition.End, animate: true);
        }
    }
}
