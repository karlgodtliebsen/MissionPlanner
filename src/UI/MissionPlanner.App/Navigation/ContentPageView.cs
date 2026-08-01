using MissionPlanner.App.Helpers;
using MissionPlanner.Library.EventHub;

namespace MissionPlanner.App.Navigation;

/// <summary>
/// Represents a content page view that is associated with a specific view model type.
/// Handles viewmodel allocation and cleanup for navigation events.
/// </summary>
public class ContentPageView<TViewModel> : ContentPage //, IDisposable
    where TViewModel : class, IDisposable

{
    private readonly string route;
    private readonly Disposables disposables = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentPageView{TViewModel}"/> class.
    /// </summary>
    protected ContentPageView(string route)
    {
        this.route = route;
        //var navigationEventHub = ServiceHelper.GetRequiredService<INavigationEventHub>();
        //disposables.Add(navigationEventHub.Subscribe(OnNavigatedEvent));
        //disposables.Add(navigationEventHub.Subscribe(OnNavigatingEvent));
    }

    ///// <summary>
    ///// Handles navigation events that are occurring and updates the view model accordingly.
    ///// </summary>
    ///// <param name="navigatingEvent">The navigation event that is occurring.</param>
    //protected virtual void OnNavigatingEvent(NavigatingEvent navigatingEvent)
    //{
    //    var source = navigatingEvent.EventArgs.Source;
    //    if (source is ShellNavigationSource.ShellContentChanged or ShellNavigationSource.ShellItemChanged or ShellNavigationSource.ShellSectionChanged)
    //    {
    //        DeactivateViewModel();
    //    }
    //}

    ///// <summary>
    ///// Handles navigation events and updates the view model accordingly.
    ///// </summary>
    ///// <param name="navigatedEvent">The navigation event.</param>
    //protected virtual void OnNavigatedEvent(NavigatedEvent navigatedEvent)
    //{
    //    var source = navigatedEvent.EventArgs.Source;
    //    if (source is ShellNavigationSource.ShellContentChanged or ShellNavigationSource.ShellItemChanged or ShellNavigationSource.ShellSectionChanged)
    //    {
    //        DeactivateViewModel();
    //        if (navigatedEvent.Current == route)
    //        {
    //            var viewModel = ServiceHelper.GetRequiredService<TViewModel>();
    //            BindingContext = viewModel;
    //        }

    //        return;
    //    }
    //}


    /// <inheritdoc />
    protected override void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        base.OnNavigatingFrom(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            DeactivateViewModel();
        }
    }

    /// <inheritdoc />
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            DeactivateViewModel();
        }
    }

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            var viewModel = ServiceHelper.GetRequiredService<TViewModel>();
            BindingContext = viewModel;
        }
    }
    //if (source is ShellNavigationSource.ShellContentChanged or ShellNavigationSource.ShellItemChanged or ShellNavigationSource.ShellSectionChanged)

    private void DeactivateViewModel()
    {
        var viewModel = BindingContext as TViewModel;
        BindingContext = null;
        viewModel?.Dispose();
    }

    ///// <inheritdoc />
    //public virtual void Dispose()
    //{
    //    //foreach (var disposable in disposables)
    //    //{
    //    //    disposable.Dispose();
    //    //}

    //    //disposables.Clear();
    //    DeactivateViewModel();
    //}
}
