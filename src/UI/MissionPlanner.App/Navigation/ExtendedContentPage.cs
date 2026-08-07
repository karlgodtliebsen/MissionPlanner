using MissionPlanner.App.Helpers;
using UraniumUI.Pages;

namespace MissionPlanner.App.Navigation;

/// <summary>
/// Represents a content page view that is associated with a specific view model type.
/// Handles viewmodel allocation and cleanup for navigation events.
/// Used to Enforce Cleanup when Navigating away from a view. The view model is automatically disposed when the view is disposed.
/// </summary>
public class ExtendedContentPage<TViewModel> : UraniumContentPage
    where TViewModel : class, IDisposable

{
    /// <summary>
    ///  
    /// </summary>
    protected TViewModel? ViewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtendedContentPage{TViewModel}"/> class.
    /// </summary>
    protected ExtendedContentPage()
    {
    }

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
            ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
            BindingContext = ViewModel;
        }
    }

    private void DeactivateViewModel()
    {
        BindingContext = null;
        ViewModel?.Dispose();
        ViewModel = null;
    }
}
