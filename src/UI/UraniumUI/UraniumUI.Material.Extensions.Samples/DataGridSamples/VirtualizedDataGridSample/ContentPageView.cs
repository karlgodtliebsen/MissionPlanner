using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.VirtualizedDataGridSample;

/// <summary>
/// Represents a content page view that is associated with a specific view model type.
/// Handles viewmodel allocation and cleanup for navigation events.
/// </summary>
public class ContentPageView<TViewModel> : UraniumContentPage
    where TViewModel : class, IDisposable

{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentPageView{TViewModel}"/> class.
    /// </summary>
    protected ContentPageView()
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
            var viewModel = ServiceHelper.GetRequiredService<TViewModel>();
            BindingContext = viewModel;
        }
    }

    private void DeactivateViewModel()
    {
        var viewModel = BindingContext as TViewModel;
        BindingContext = null;
        viewModel?.Dispose();
    }
}
