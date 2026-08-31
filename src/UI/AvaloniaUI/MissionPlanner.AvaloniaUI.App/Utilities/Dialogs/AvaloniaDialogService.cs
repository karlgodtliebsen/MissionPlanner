using Avalonia.Controls;
using MissionPlanner.AvaloniaUI.App.Utilities.Dispatching;
using Ursa.Controls;

namespace MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;

/// <summary>
/// Provides dialog services for displaying various types of dialogs and windows in an Avalonia application.
/// </summary>
public sealed class AvaloniaDialogService : IDialogService
{
    private readonly IUiDispatcher uiDispatcher;
    private readonly IWindowProvider windowProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaDialogService"/> class.
    /// </summary>
    /// <param name="uiDispatcher">The UI dispatcher.</param>
    /// <param name="windowProvider">The window provider.</param>
    public AvaloniaDialogService(IUiDispatcher uiDispatcher, IWindowProvider windowProvider)
    {
        this.uiDispatcher = uiDispatcher;
        this.windowProvider = windowProvider;
    }


    /// <inheritdoc />
    public async Task<bool> ShowAsync(Control content, DialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);

        return options.Presentation switch
        {
            DialogPresentation.Window =>
                await ShowWindowAsync(content, options),

            DialogPresentation.Overlay =>
                await ShowOverlayAsync(content, options),

            _ => throw new ArgumentOutOfRangeException(nameof(options.Presentation), options.Presentation, "Unknown dialog presentation.")
        };
    }


    /// <inheritdoc />
    public Task<bool> ShowWindowAsync(
        Control content,
        DialogOptions options)
    {
        return uiDispatcher.DispatchAsync(async () =>
        {
            var owner = windowProvider.ActiveWindow
                        ?? throw new InvalidOperationException(
                            "No active window is available.");

            var dialog = new ViewDialogWindow
            {
                Title = options.Title,
                Width = options.Width ?? 800,
                Height = options.Height ?? 600,
                CanResize = options.CanResize
            };

            dialog.DataContext =
                new ViewDialogViewModel(
                    options.Title,
                    content,
                    options.OkText,
                    options.CloseText,
                    options.ShowOkButton,
                    options.ShowCloseButton,
                    result => dialog.Close(result));

            return await dialog.ShowDialog<bool>(owner);
        });
    }

    /// <inheritdoc />
    public Task<bool> ShowOverlayAsync(
        Control content,
        DialogOptions options)
    {
        return uiDispatcher.DispatchAsync(async () =>
        {
            var owner = windowProvider.ActiveWindow
                        ?? throw new InvalidOperationException("No active window is available.");

            var vm =
                new OverlayViewDialogViewModel(
                    options.Title,
                    content,
                    options.OkText,
                    options.CloseText,
                    options.ShowOkButton,
                    options.ShowCloseButton);

            var view = new OverlayViewDialog();

            if (options.Width is not null)
            {
                view.Width = options.Width.Value;
            }

            if (options.Height is not null)
            {
                view.Height = options.Height.Value;
            }

            var overlayOptions =
                new OverlayDialogOptions
                {
                    IsCloseButtonVisible = true,

                    CanLightDismiss =
                        options.CanLightDismiss,

                    CanResize =
                        options.CanResize,

                    CanDragMove = true,

                    //
                    // Important if MissionPlanner ever has
                    // multiple UrsaWindows.
                    //
                    TopLevelHashCode =
                        owner.GetHashCode()
                };

            var result =
                await OverlayDialog.ShowCustomAsync<bool>(
                    view,
                    vm,
                    hostId: null,
                    options: overlayOptions);

            return result == true;
        });
    }

    /// <inheritdoc/>
    public Task<IDisposable> DisplayProgressCancellableAsync(string title, Func<string> message, string cancelText = "Cancel", CancellationTokenSource? tokenSource = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<string?> DisplayPromptAsync(string title, string message, string initialValue, string accept = "OK", string cancel = "Cancel", string clear = "Clear")
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<TimeSpan?> DisplayPromptAsync(string title, string message, TimeSpan? initialValue = null, TimeSpan? minimumTime = null, TimeSpan? maximumTime = null, string accept = "OK", string cancel = "Cancel", string clear = "Clear")
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<int?> DisplayPromptAsync(string title, string message, int? initialValue = null, int? minimum = null, int? maximum = null, string accept = "OK", string cancel = "Cancel", string clear = "Clear")
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<long?> DisplayPromptAsync(string title, string message, long? initialValue = null, long? minimum = null, long? maximum = null, string accept = "OK", string cancel = "Cancel", string clear = "Clear")
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<float?> DisplayPromptAsync(string title, string message, float? initialValue = null, float? minimum = null, float? maximum = null, string accept = "OK", string cancel = "Cancel", string clear = "Clear")
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<double?> DisplayPromptAsync(string title, string message, double? initialValue = null, double? minimum = null, double? maximum = null, string accept = "OK", string cancel = "Cancel", string clear = "Clear")
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<T> DisplayRadioButtonPromptAsync<T>(string message, IEnumerable<T> selectionSource, T selected = default(T), string accept = "Ok", string cancel = "Cancel", string? displayMember = null)
    {
        throw new NotImplementedException();
    }

}
