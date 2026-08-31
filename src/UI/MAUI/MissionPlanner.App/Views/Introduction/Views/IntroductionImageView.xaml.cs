using MissionPlanner.App.Views.Introduction.Models;
using MissionPlanner.App.Views.Introduction.Services;

namespace MissionPlanner.App.Views.Introduction.Views;

/// <summary>
/// 
/// </summary>
public partial class IntroductionImageView : ContentView, IDisposable
{
    private CancellationTokenSource? loadCancellation;
    private int loadVersion;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntroductionImageView"/> class.
    /// </summary>
    public IntroductionImageView()
    {
        InitializeComponent();
        BindingContextChanged += OnBindingContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        BindingContextChanged -= OnBindingContextChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        CancelPendingLoad();
        ScreenshotImage.Source = null;
    }

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        CancelPendingLoad();
        ScreenshotImage.Source = null;
        SetLoading(false);

        if (IsLoaded)
        {
            StartImageLoad();
        }
    }

    private void OnLoaded(object? sender, EventArgs e) => StartImageLoad();

    private void OnUnloaded(object? sender, EventArgs e)
    {
        CancelPendingLoad();
        ScreenshotImage.Source = null;
        SetLoading(false);
    }

    private void StartImageLoad()
    {
        if (disposed || !IsLoaded || BindingContext is not IntroductionImage image || string.IsNullOrWhiteSpace(image.Path))
        {
            return;
        }

        CancelPendingLoad();
        var cancellation = new CancellationTokenSource();
        loadCancellation = cancellation;
        var version = ++loadVersion;
        SetLoading(true);
        _ = LoadImageAsync(image.Path, cancellation, version);
    }

    private async Task LoadImageAsync(string path, CancellationTokenSource cancellation, int version)
    {
        try
        {
            var source = await IntroductionAssetLoader.LoadImageSourceAsync(path, cancellation.Token);

            if (CanApply(cancellation, version))
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
            if (ReferenceEquals(loadCancellation, cancellation))
            {
                loadCancellation = null;
                cancellation.Dispose();
                if (!disposed && IsLoaded)
                {
                    SetLoading(false);
                }
            }
        }
    }

    private bool CanApply(CancellationTokenSource cancellation, int version) =>
        !disposed && IsLoaded && !cancellation.IsCancellationRequested &&
        version == loadVersion && ReferenceEquals(loadCancellation, cancellation);

    private void CancelPendingLoad()
    {
        loadVersion++;
        var cancellation = loadCancellation;
        loadCancellation = null;
        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void SetLoading(bool value)
    {
        LoadingIndicator.IsRunning = value;
        LoadingIndicator.IsVisible = value;
    }
}
