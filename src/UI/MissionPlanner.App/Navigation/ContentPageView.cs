using MissionPlanner.App.Helpers;
using MissionPlanner.App.Helpers.Navigation;
using MissionPlanner.Library.EventHub;

namespace MissionPlanner.App.Navigation;

/// <summary>
/// Interaction logic for FullParametersListTabView.xaml
/// </summary>
public class ContentPageView<TViewModel> : ContentPage, IDisposable
    where TViewModel : class, IDisposable

{
    private readonly string route;
    private TViewModel? viewModel;
    private readonly Disposables disposables = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentPageView{TViewModel}"/> class.
    /// </summary>
    protected ContentPageView(string route)
    {
        this.route = route;
        var navigationEventHub = ServiceHelper.GetRequiredService<INavigationEventHub>();
        disposables.Add(navigationEventHub.Subscribe(OnNavigatedEvent));
        disposables.Add(navigationEventHub.Subscribe(OnNavigatingEvent));
    }

    /// <summary>
    /// Handles navigation events that are occurring and updates the view model accordingly.
    /// </summary>
    /// <param name="navigatingEvent">The navigation event that is occurring.</param>
    protected virtual void OnNavigatingEvent(NavigatingEvent navigatingEvent)
    {
    }

    /// <summary>
    /// Handles navigation events and updates the view model accordingly.
    /// </summary>
    /// <param name="navigatedEvent">The navigation event.</param>
    protected virtual void OnNavigatedEvent(NavigatedEvent navigatedEvent)
    {
        var isSubNavigation =
            (navigatedEvent.Previous == route && navigatedEvent.Current?.StartsWith(route + "/") == true)
            || (navigatedEvent.Current == route && navigatedEvent.Previous?.StartsWith(route + "/") == true);
        if (isSubNavigation)
        {
            return;
        }

        if (navigatedEvent.Previous == route)
        {
            BindingContext = null;
            viewModel?.Dispose();
            viewModel = null;
        }

        if (navigatedEvent.Current == route)
        {
            BindingContext = null;
            viewModel?.Dispose();
            viewModel = ServiceHelper.GetRequiredService<TViewModel>();
            BindingContext = viewModel;
        }
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }

        disposables.Clear();
        viewModel?.Dispose();
        viewModel = null;
    }
}
