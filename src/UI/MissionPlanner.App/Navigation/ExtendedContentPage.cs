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
    private readonly string? key;

    /// <summary>
    ///  
    /// </summary>
    protected TViewModel? ViewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtendedContentPage{TViewModel}"/> class.
    /// </summary>
    protected ExtendedContentPage(string? k = null)
    {
        key = k;
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

    /// <inheritdoc/>
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (args.NavigationType is not (NavigationType.Replace or NavigationType.Remove))
        {
            return;
        }

        ViewModel = key is not null
            ? ServiceHelper.GetRequiredKeyedService<TViewModel>(key)
            : ServiceHelper.GetRequiredService<TViewModel>();

        BindingContext = ViewModel;
        try
        {
            await OnModelCreatedAsync(ViewModel);
        }
        catch (OperationCanceledException)
        {
            // Expected if navigation deactivates the model during activation.
        }
    }

    /// <summary>
    /// Called when the view model is created.
    /// </summary>
    /// <param name="viewModel">The created view model.</param>
    protected virtual Task OnModelCreatedAsync(TViewModel viewModel)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the view model is being destroyed.
    /// </summary>
    /// <param name="viewModel">The view model being destroyed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected virtual void OnDestroyingModel(TViewModel viewModel)
    {
    }

    private void DeactivateViewModel()
    {
        var viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }

        // Claim the model immediately. OnNavigatedFrom will then be a no-op
        // if OnNavigatingFrom already performed cleanup.
        ViewModel = null;
        BindingContext = null;

        try
        {
            OnDestroyingModel(viewModel);
        }
        finally
        {
            viewModel.Dispose();
        }
    }
}
