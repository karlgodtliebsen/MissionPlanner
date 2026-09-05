using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MissionPlanner.AvaloniaUI.App.Views.Introduction.Models;
using MissionPlanner.AvaloniaUI.App.Views.Introduction.Services;

namespace MissionPlanner.AvaloniaUI.App.Views.Introduction.Views;

/// <summary>Loads and displays a bundled introduction image.</summary>
public partial class IntroductionImageView : UserControl, IDisposable
{
    private CancellationTokenSource? loadCancellation;
    private int loadVersion;
    private bool disposed;

    /// <summary>Initializes the image view.</summary>
    public IntroductionImageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        DataContextChanged -= OnDataContextChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        CancelPendingLoad();
        ScreenshotImage.Source = null;
    }

    private void OnDataContextChanged(object? sender, EventArgs args)
    {
        CancelPendingLoad();
        ScreenshotImage.Source = null;
        LoadingIndicator.IsVisible = false;
        if (IsLoaded) StartImageLoad();
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs args) => StartImageLoad();

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        CancelPendingLoad();
        ScreenshotImage.Source = null;
        LoadingIndicator.IsVisible = false;
    }

    private void StartImageLoad()
    {
        if (disposed || !IsLoaded || DataContext is not IntroductionImage image || string.IsNullOrWhiteSpace(image.Path)) return;
        CancelPendingLoad();
        var cancellation = new CancellationTokenSource();
        loadCancellation = cancellation;
        var version = ++loadVersion;
        LoadingIndicator.IsVisible = true;
        _ = LoadImageAsync(image.Path, cancellation, version);
    }

    private async Task LoadImageAsync(string path, CancellationTokenSource cancellation, int version)
    {
        try
        {
            var bytes = await IntroductionAssetLoader.ReadBytesAsync(path, cancellation.Token);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (CanApply(cancellation, version)) ScreenshotImage.Source = new Bitmap(new MemoryStream(bytes));
            });
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch
        {
            // A missing optional screenshot must not make the Introduction unusable.
        }
        finally
        {
            if (ReferenceEquals(loadCancellation, cancellation))
            {
                loadCancellation = null;
                cancellation.Dispose();
                if (!disposed) LoadingIndicator.IsVisible = false;
            }
        }
    }

    private bool CanApply(CancellationTokenSource cancellation, int version) =>
        !disposed && IsLoaded && !cancellation.IsCancellationRequested && version == loadVersion &&
        ReferenceEquals(loadCancellation, cancellation);

    private void CancelPendingLoad()
    {
        loadVersion++;
        var cancellation = loadCancellation;
        loadCancellation = null;
        if (cancellation is null) return;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
