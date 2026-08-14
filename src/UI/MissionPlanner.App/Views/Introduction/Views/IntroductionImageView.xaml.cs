using MissionPlanner.App.Views.Introduction.Models;
using MissionPlanner.App.Views.Introduction.Services;

namespace MissionPlanner.App.Views.Introduction.Views;

/// <summary>
/// 
/// </summary>
public partial class IntroductionImageView : ContentView, IDisposable
{
    private CancellationTokenSource? loadCancellation;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntroductionImageView"/> class.
    /// </summary>
    public IntroductionImageView()
    {
        InitializeComponent();
        loadCancellation = new CancellationTokenSource();
        BindingContextChanged += OnBindingContextChanged;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        BindingContextChanged -= OnBindingContextChanged;
    }

    private async void OnBindingContextChanged(object? sender, EventArgs e)
    {
        if (loadCancellation is null)
        {
            return;
        }

        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = new CancellationTokenSource();

        ScreenshotImage.Source = null;

        if (BindingContext is not IntroductionImage image || string.IsNullOrWhiteSpace(image.Path))
        {
            return;
        }

        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;

        try
        {
            var source = await IntroductionAssetLoader.LoadImageSourceAsync(image.Path, loadCancellation.Token);

            if (!loadCancellation.IsCancellationRequested)
            {
                ScreenshotImage.Source = source;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal when the user changes topic before the image finishes loading.
        }
        catch (Exception)
        {
            // Keep the Introduction usable even when a screenshot is not yet present.
            ScreenshotImage.Source = null;
        }
        finally
        {
            if (!loadCancellation.IsCancellationRequested)
            {
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
            }
        }
    }
}
