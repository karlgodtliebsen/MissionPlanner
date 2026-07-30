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
        if (IsModalTransition(
                navigatingEvent.Previous,
                navigatingEvent.Current))
        {
            return;
        }

        var isSubNavigation =
            navigatingEvent.Previous == route &&
            navigatingEvent.Current?.StartsWith(
                route + "/",
                StringComparison.Ordinal) == true;

        if (navigatingEvent.Previous == route &&
            navigatingEvent.Current != route &&
            !isSubNavigation)
        {
            DeactivateViewModel();
        }
    }

    /// <summary>
    /// Handles navigation events and updates the view model accordingly.
    /// </summary>
    /// <param name="navigatedEvent">The navigation event.</param>
    protected virtual void OnNavigatedEvent(NavigatedEvent navigatedEvent)
    {
        if (IsModalTransition(
                navigatedEvent.Previous,
                navigatedEvent.Current))
        {
            return;
        }

        var isSubNavigation =
            (navigatedEvent.Previous == route && navigatedEvent.Current?.StartsWith(route + "/") == true)
            || (navigatedEvent.Current == route && navigatedEvent.Previous?.StartsWith(route + "/") == true);
        if (isSubNavigation)
        {
            return;
        }

        if (navigatedEvent.Previous == route)
        {
            DeactivateViewModel();
        }

        if (navigatedEvent.Current == route)
        {
            BindingContext = null;
            viewModel?.Dispose();
            viewModel = ServiceHelper.GetRequiredService<TViewModel>();
            BindingContext = viewModel;
        }
    }

    private static bool IsModalTransition(string? previous, string? current)
    {
        if (previous is null || current is null)
        {
            return false;
        }

        // ShellContent destinations in this application are absolute routes.
        // Modal pages pushed through INavigation use generated relative routes
        // such as D_FAULT_DefaultDialogAnimatedContentPage43. A transition
        // between those route kinds is an overlay opening or closing, not a
        // departure from or activation of the underlying Shell page.
        return IsShellRoute(previous) != IsShellRoute(current);
    }

    private static bool IsShellRoute(string location)
    {
        return location.StartsWith("//", StringComparison.Ordinal);
    }

    private void DeactivateViewModel()
    {
        BindingContext = null;
        viewModel?.Dispose();
        viewModel = null;
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }

        disposables.Clear();
        DeactivateViewModel();
    }
}
