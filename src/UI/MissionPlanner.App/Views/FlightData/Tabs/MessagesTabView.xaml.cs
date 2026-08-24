using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>
/// 
/// </summary>
public partial class MessagesTabView : TabViewLifecycleContent<MessagesTabViewModel>, IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessagesTabView"/> class.
    /// </summary>
    public MessagesTabView()
    {
        InitializeComponent();
        ViewModel?.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        await base.ActivateAsync();
        ViewModel?.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        ViewModel?.PropertyChanged -= OnViewModelPropertyChanged;
        await base.DeactivateAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DeactivateAsync().GetAwaiter().GetResult();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MessagesTabViewModel.ScrollRequestVersion) &&
            ViewModel is { IsAutoScrollPaused: false } &&
            ViewModel.Items.LastOrDefault() is { } last)
        {
            //MessageCollection.ScrollTo(last, position: ScrollToPosition.End, animate: true);
        }
    }


}
