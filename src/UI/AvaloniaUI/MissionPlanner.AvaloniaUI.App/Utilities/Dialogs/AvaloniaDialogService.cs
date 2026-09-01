using System.Globalization;
using Avalonia.Controls;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs.SubViews;
using MissionPlanner.AvaloniaUI.App.Utilities.Dispatching;
using Ursa.Controls;
using PromptInputDialogView = MissionPlanner.AvaloniaUI.App.Utilities.Dialogs.SubViews.PromptInputDialogView;
using PromptInputDialogViewModel = MissionPlanner.AvaloniaUI.App.Utilities.Dialogs.SubViews.PromptInputDialogViewModel;

namespace MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;

/// <summary>Displays reusable view/view-model dialog content in a common window or overlay shell.</summary>
public sealed class AvaloniaDialogService(IUiDispatcher dispatcher, IWindowProvider windowProvider) : IDialogService
{
    private readonly Lock openWindowsLock = new();
    private readonly List<ViewDialogWindow> openWindows = [];

    public Task<bool> ShowAsync(Control content, DialogOptions options, CancellationToken cancellationToken = default)
    {
        return options.Presentation switch
        {
            DialogPresentation.Window => ShowWindowAsync(content, options, cancellationToken),
            DialogPresentation.Overlay => ShowOverlayAsync(content, options, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(options.Presentation), options.Presentation, "Unknown dialog presentation.")
        };
    }

    public Task<bool> ShowWindowAsync(Control content, DialogOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);
        return dispatcher.DispatchAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var owner = windowProvider.ActiveWindow ?? throw new InvalidOperationException("No active window is available.");
            var dialog = CreateWindow(content, options);
            using var registration = cancellationToken.Register(() => dispatcher.Dispatch(() => dialog.Close(false)));
            Register(dialog);
            try
            {
                return await dialog.ShowDialog<bool>(owner);
            }
            finally
            {
                Unregister(dialog);
            }
        });
    }

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

    public Task<bool> ShowOverlayAsync(Control content, DialogOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(options);
        return dispatcher.DispatchAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var owner = windowProvider.ActiveWindow ?? throw new InvalidOperationException("No active window is available.");
            var viewModel = new OverlayViewDialogViewModel(options.Title, content, options.OkText, options.CloseText,
                options.ShowOkButton, options.ShowCloseButton);
            var view = new OverlayViewDialog();
            if (options.Width is { } width)
            {
                view.Width = width;
            }

            if (options.Height is { } height)
            {
                view.Height = height;
            }

            var overlayOptions = new OverlayDialogOptions
            {
                IsCloseButtonVisible = true,
                CanLightDismiss = options.CanLightDismiss,
                CanResize = options.CanResize,
                CanDragMove = true,
                TopLevelHashCode = owner.GetHashCode()
            };
            using var registration = cancellationToken.Register(() => dispatcher.Dispatch(viewModel.Close));
            return await OverlayDialog.ShowCustomAsync<bool>(view, viewModel, null, overlayOptions) == true;
        });
    }

    public Task<bool> ConfirmAsync(string message, DialogOptions options, CancellationToken cancellationToken = default)
    {
        return ShowWindowAsync(new SubViews.ConfirmDialogView(new ConfirmDialogViewModel(message)), Compact(options), cancellationToken);
    }

    public async Task<string?> PromptAsync(DialogOptions options, string? message, string? initialValue = null, CancellationToken cancellationToken = default)
    {
        var viewModel = new PromptInputDialogViewModel(initialValue, message);
        var accepted = await ShowWindowAsync(new PromptInputDialogView(viewModel), Compact(options), cancellationToken);
        return accepted ? viewModel.PromptText : null;
    }

    public async Task<string?> ChooseAsync(DialogOptions options, IReadOnlyList<string> choices, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (choices.Count == 0)
        {
            return null;
        }

        var viewModel = new SubViews.ChoiceDialogViewModel(choices);
        var accepted = await ShowWindowAsync(new SubViews.ChoiceDialogView(viewModel), Compact(options), cancellationToken);
        return accepted ? viewModel.SelectedChoice : null;
    }

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

    public Task<string?> DisplayPromptAsync(string title, string message, string initialValue, string accept = "OK", string cancel = "Cancel", string clear = "Clear")
    {
        return PromptAsync(PromptOptions(title, accept, cancel), message, initialValue);
    }

    public async Task<int?> PromptAsync(DialogOptions options, string? message, int initialValue, int minimum, int maximum, CancellationToken cancellationToken = default)
    {
        var text = await PromptAsync(options, message, initialValue.ToString(CultureInfo.CurrentCulture), cancellationToken);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var value)
               && value >= minimum && value <= maximum ? value : null;
    }

    public Task<int?> DisplayPromptAsync(string title, string message, int initialValue, int minimum, int maximum, string accept = "OK", string cancel = "Cancel", string clear = "Clear")
    {
        return PromptAsync(PromptOptions(title, accept, cancel), message, initialValue, minimum, maximum);
    }

    public async Task<double?> DisplayPromptAsync(string title, string message, double initialValue, double? minimum = null, double? maximum = null, string accept = "OK", string cancel = "Cancel", string clear = "Clear")
    {
        var text = await PromptAsync(PromptOptions(title, accept, cancel), message,
            initialValue.ToString(CultureInfo.CurrentCulture));
        return !double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value)
            || (minimum is { } min && value < min) || (maximum is { } max && value > max)
            ? null
            : value;
    }

    private static DialogOptions PromptOptions(string title, string accept, string cancel)
    {
        return new()
        {
            Title = title,
            Width = 460,
            Height = 220,
            CanResize = false,
            OkText = accept,
            CloseText = cancel
        };
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
