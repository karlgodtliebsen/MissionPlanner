using Avalonia.Controls;
using MissionPlanner.App.Utilities.Dialogs.SubViews;
using MissionPlanner.App.Utilities.Dispatching;
using Ursa.Controls;

namespace MissionPlanner.App.Utilities.Dialogs;

/// <summary>Displays reusable view/view-model dialog content in a common window or overlay shell.</summary>
public sealed class AvaloniaDialogService(IUiDispatcher dispatcher, IWindowProvider windowProvider) : IDialogService
{
    private readonly Lock openWindowsLock = new();
    private readonly List<ViewDialogWindow> openWindows = [];

    /// <summary>
    /// Creates overlay dialog options with the specified title, accept text, and cancel text.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="accept">The text for the accept button.</param>
    /// <param name="cancel">The text for the cancel button.</param>
    /// <returns>The created overlay dialog options.</returns>
    public OverlayDialogOptions CreateOptions(string title, string? accept = null, string? cancel = null)
    {
        return CreateDialogOptions(title, accept, cancel);
    }

    /// <inheritdoc/>
    public static OverlayDialogOptions CreateDialogOptions(string title, string? accept, string? cancel)
    {
        var options = new OverlayDialogOptions()
        {
            FullScreen = false,
            HorizontalAnchor = HorizontalPosition.Center,
            VerticalAnchor = VerticalPosition.Center,
            HorizontalOffset = null,
            VerticalOffset = null,
            Title = title,
            CanLightDismiss = true,
            CanDragMove = true,
            IsCloseButtonVisible = true,//!string.IsNullOrEmpty(accept),
            Buttons = DialogButton.OKCancel,
            CanResize = true,
        };
        if (string.IsNullOrEmpty(cancel))
        {
            //options.Buttons = DialogButton.OK;
        }
        return options;
    }


    /// <inheritdoc/>
    public async Task<TViewModel> ShowOverlayDialogAsync<TView, TViewModel>(TViewModel model, OverlayDialogOptions options, string? overLayHost = null, CancellationToken cancellationToken = default)
        where TView : UserControl, new()
        where TViewModel : DialogViewModelBase
    {
        return await dispatcher.DispatchAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            model.Title = options.Title ?? "";
            await OverlayDialog.ShowCustomAsync<TView, TViewModel, bool>(model, overLayHost, options: options, token: cancellationToken);
            return model;
        });
    }

    /// <inheritdoc/>
    public TViewModel ShowOverlayDialog<TView, TViewModel>(TViewModel model, OverlayDialogOptions options, string? overLayHost = null)
        where TView : UserControl, new()
        where TViewModel : DialogViewModelBase
    {
        dispatcher.Dispatch(() =>
        {
            model.Title = options.Title ?? "";
            OverlayDialog.ShowCustom<TView, TViewModel>(model, overLayHost, options: options);
        });
        return model;
    }


    /// <inheritdoc/>
    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        return dispatcher.DispatchAsync(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        ViewDialogWindow? dialog;
        lock (openWindowsLock)
        {
            dialog = openWindows.LastOrDefault();
        }
        dialog?.Close(false);
    });
    }


    /// <inheritdoc/>
    public async Task<bool> ConfirmAsync(OverlayDialogOptions options, string message, CancellationToken cancellationToken = default)
    {
        return await dispatcher.DispatchAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ShowOverlayDialogAsync<ConfirmDialogView, ConfirmDialogViewModel>(new ConfirmDialogViewModel(message), options, cancellationToken: cancellationToken);
            return result.Confirmation;
        });
    }

    /// <inheritdoc/>
    public async Task<string?> PromptAsync(OverlayDialogOptions options, string? message, string? initialValue = null, CancellationToken cancellationToken = default)
    {
        return await dispatcher.DispatchAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var viewModel = new PromptInputDialogViewModel(initialValue, message);
            var result = await ShowOverlayDialogAsync<PromptInputDialogView, PromptInputDialogViewModel>(viewModel, options, cancellationToken: cancellationToken);
            return result.Confirmation ? result.PromptText : null;
        });
    }
    /// <inheritdoc/>
    public Task<string?> PromptAsync(string title, string message, string initialValue, string accept = "OK", string cancel = "Cancel", string clear = "Clear", CancellationToken cancellationToken = default)
    {
        var options = CreateDialogOptions(title, accept, cancel);
        return PromptAsync(options, message, initialValue, cancellationToken);
    }


    /// <inheritdoc/>
    public async Task<string?> ChooseAsync(OverlayDialogOptions options, IReadOnlyList<string> choices, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (choices.Count == 0)
        {
            return null;
        }
        var viewModel = new ChoiceDialogViewModel(choices);
        var result = await ShowOverlayDialogAsync<ChoiceDialogView, ChoiceDialogViewModel>(viewModel, options, cancellationToken: cancellationToken);
        return result.Confirmation ? result.SelectedChoice : null;
    }


    /// <inheritdoc/>
    public async Task<int?> PromptAsync(OverlayDialogOptions options, string? message, int initialValue, int minimum, int maximum, CancellationToken cancellationToken = default)
    {
        var viewModel = new PromptIntDialogViewModel(options.Title ?? "", message ?? "", initialValue, minimum, maximum);
        var result = await ShowOverlayDialogAsync<PromptIntDialogView, PromptIntDialogViewModel>(viewModel, options, cancellationToken: cancellationToken);
        if (!result.Confirmation)
        {
            return null;
        }

        var value = result.Value;
        return
            (minimum is { } min && value < min) || (maximum is { } max && value > max)
                ? null
                : value;
    }

    /// <inheritdoc/>
    public async Task<int?> PromptAsync(string title, string message, int initialValue, int minimum, int maximum, string accept = "OK", string cancel = "Cancel",
        string clear = "Clear", CancellationToken cancellationToken = default)
    {
        var options = CreateDialogOptions(title, accept, cancel);

        var viewModel = new PromptIntDialogViewModel(title, message, initialValue, minimum, maximum);
        var result = await ShowOverlayDialogAsync<PromptIntDialogView, PromptIntDialogViewModel>(viewModel, options, cancellationToken: cancellationToken);
        if (!result.Confirmation)
        {
            return null;
        }

        var value = result.Value;
        return
            (minimum is { } min && value < min) || (maximum is { } max && value > max)
                ? null
                : value;

    }

    /// <inheritdoc/>
    public async Task<double?> PromptAsync(string title, string message, double initialValue, double? minimum = null, double? maximum = null, string accept = "OK", string cancel = "Cancel", string clear = "Clear", CancellationToken cancellationToken = default)
    {
        var options = CreateDialogOptions(title, accept, cancel);
        var viewModel = new PromptDoubleDialogViewModel(title, message, initialValue, minimum, maximum);
        var result = await ShowOverlayDialogAsync<PromptDoubleDialogView, PromptDoubleDialogViewModel>(viewModel, options, cancellationToken: cancellationToken);
        if (!result.Confirmation)
        {
            return null;
        }

        var value = result.Value;
        return
             (minimum is { } min && value < min) || (maximum is { } max && value > max)
            ? null
            : value;
    }

    /// <inheritdoc/>
    public async Task<double?> PromptAsync(OverlayDialogOptions options, string? message, double initialValue, double? minimum = null, double? maximum = null, CancellationToken cancellationToken = default)
    {
        var viewModel = new PromptDoubleDialogViewModel(options.Title ?? "", message ?? "", initialValue, minimum, maximum);
        var result = await ShowOverlayDialogAsync<PromptDoubleDialogView, PromptDoubleDialogViewModel>(viewModel, options, cancellationToken: cancellationToken);
        if (!result.Confirmation)
        {
            return null;
        }

        var value = result.Value;
        return
            (minimum is { } min && value < min) || (maximum is { } max && value > max)
                ? null
                : value;
    }

    /// <inheritdoc/>
    public Task<IDisposable> DisplayProgressCancellableAsync(Func<string> message, DialogOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return dispatcher.DispatchAsync<IDisposable>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var owner = windowProvider.ActiveWindow ?? throw new InvalidOperationException("No active window is available.");
            var contentViewModel = new SubViews.ProgressDialogViewModel(message);
            var effectiveOptions = Compact(options) with
            {
                Height = 180,
                ShowOkButton = false,
                ShowCloseButton = false,
                CanResize = false
            };
            var dialog = CreateWindow(new SubViews.ProgressDialogView(contentViewModel), effectiveOptions);
            var registration = cancellationToken.Register(() => dispatcher.Dispatch(() => dialog.Close(false)));
            Register(dialog);
            _ = dialog.ShowDialog<bool>(owner).ContinueWith(_ =>
            {
                Unregister(dialog);
                contentViewModel.Dispose();
            }, TaskScheduler.Default);
            return new DialogHandle(() => dispatcher.Dispatch(() => dialog.Close(false)), registration);
        });
    }


    private static DialogOptions Compact(DialogOptions options)
    {
        return options with
        {
            Width = options.Width is null or 800 ? 460 : options.Width,
            Height = options.Height is null or 600 ? 220 : options.Height,
            CanResize = false
        };
    }

    private static ViewDialogWindow CreateWindow(Control content, DialogOptions options)
    {
        var dialog = new ViewDialogWindow
        {
            Title = options.Title,
            Width = options.Width ?? 800,
            Height = options.Height ?? 600,
            CanResize = options.CanResize
        };
        dialog.DataContext = new ViewDialogViewModel(options.Title, content, options.OkText, options.CloseText,
            options.ShowOkButton, options.ShowCloseButton, result => dialog.Close(result));
        return dialog;
    }

    private void Register(ViewDialogWindow dialog)
    {
        lock (openWindowsLock)
        {
            openWindows.Add(dialog);
        }
    }

    private void Unregister(ViewDialogWindow dialog)
    {
        lock (openWindowsLock)
        {
            openWindows.Remove(dialog);
        }
    }

    private sealed class DialogHandle(Action close, CancellationTokenRegistration registration) : IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            registration.Dispose();
            close();
        }
    }
}
