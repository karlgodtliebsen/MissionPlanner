using MissionPlanner.App.Helpers;
using MissionPlanner.Library.EventHub;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

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
        var disposable = navigationEventHub.Subscribe(OnNavigationEvent);
        disposables.Add(disposable);
    }

    /// <summary>
    /// Handles navigation events and updates the view model accordingly.
    /// </summary>
    /// <param name="navigationEvent">The navigation event.</param>
    protected virtual void OnNavigationEvent(NavigationEvent navigationEvent)
    {
        var isSubNavigation =
            (navigationEvent.Previous == route && navigationEvent.Current?.StartsWith(route + "/") == true)
            || (navigationEvent.Current == route && navigationEvent.Previous?.StartsWith(route + "/") == true);
        if (isSubNavigation)
        {
            return;
        }

        if (navigationEvent.Current == route)
        {
            viewModel?.Dispose();
            viewModel = ServiceHelper.GetRequiredService<TViewModel>();
            BindingContext = viewModel;
        }

        if (navigationEvent.Previous == route)
        {
            viewModel?.Dispose();
            viewModel = null;
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
