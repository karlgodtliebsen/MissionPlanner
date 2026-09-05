using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>
/// 
/// </summary>
public partial class MessagesTabView : TabViewLifecycleContent<MessagesTabViewModel>, IDisposable
{
    private bool isSubscribed;
    /// <summary>
    /// Initializes a new instance of the <see cref="MessagesTabView"/> class.
    /// </summary>
    public MessagesTabView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        await base.ActivateAsync();
        if (!isSubscribed && ViewModel is not null)
        {
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            isSubscribed = true;
        }
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        Unsubscribe();
        await base.DeactivateAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || ViewModel is null)
        {
            return;
        }
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        isSubscribed = false;
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
