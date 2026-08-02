using System.Diagnostics;
using System.Globalization;
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
        Debug.Print($"Navigating From Enter: {DateTimeOffset.UtcNow.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}");
        base.OnNavigatingFrom(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            DeactivateViewModel();
        }

        Debug.Print($"Navigating From Exit: {DateTimeOffset.UtcNow.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}");
    }

    /// <inheritdoc />
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        Debug.Print($"Navigated From Enter: {DateTimeOffset.UtcNow.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}");
        base.OnNavigatedFrom(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            DeactivateViewModel();
        }

        Debug.Print($"Navigated From Exit: {DateTimeOffset.UtcNow.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}");
    }


    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        Debug.Print($"Navigated To Enter: {DateTimeOffset.UtcNow.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}");
        base.OnNavigatedTo(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            if (BindingContext is null)
            {
                var viewModel = ServiceHelper.GetRequiredService<TViewModel>();
                BindingContext = viewModel;
            }
        }

        Debug.Print($"Navigated To Exit: {DateTimeOffset.UtcNow.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}");
    }

    private void DeactivateViewModel()
    {
        Debug.Print($"DeactivateViewModel Enter: {DateTimeOffset.UtcNow.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}");
        var viewModel = BindingContext as TViewModel;
        BindingContext = null;
        viewModel?.Dispose();
        Debug.Print($"DeactivateViewModel Exit: {DateTimeOffset.UtcNow.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}");
    }
}
