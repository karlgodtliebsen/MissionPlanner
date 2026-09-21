using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dialogs.SubViews;
using MissionPlanner.App.Utilities.Dispatching;
using Ursa.Controls;

namespace MissionPlanner.App.Utilities.Dialogs;

/// <summary>Displays reusable view/view-model dialog content in a common window or overlay shell.</summary>
public sealed class AvaloniaDialogService(IUiDispatcher dispatcher, IWindowProvider windowProvider, ILogger<AvaloniaDialogService>? logger = null) : IDialogService
{
    private readonly Lock openProgressDialogsLock = new();
    private readonly List<ProgressDialogViewModel> openProgressDialogs = [];

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

    /// <summary>
    /// Creates overlay dialog options with the specified title, accept text, and cancel text.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="accept">The text for the accept button.</param>
    /// <param name="cancel">The text for the cancel button.</param>
    /// <returns>The created overlay dialog options.</returns>
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
            IsCloseButtonVisible = true,
            Buttons = DialogButton.OKCancel,
            CanResize = true,
        };
        if (string.IsNullOrEmpty(cancel))
        {
            options.Buttons = DialogButton.OK;
        }
        return options;
    }

    /// <inheritdoc/>
    public async Task<TViewModel?> ShowStandardAsync<TView, TViewModel>(TViewModel model, OverlayDialogOptions options, string? overLayHost = null, CancellationToken cancellationToken = default)
        where TView : UserControl, new()
        where TViewModel : ViewModelBase
    {
        return await dispatcher.DispatchAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            //Task<DialogResult> ShowStandardAsync<TView, TViewModel>(TViewModel vm, string? hostId = null, OverlayDialogOptions? options = null, CancellationToken? token = null) where TView : Control, new()
            var dialogResult = await OverlayDialog.ShowStandardAsync<TView, TViewModel>(model, overLayHost, options: options, token: cancellationToken);
            return dialogResult is DialogResult.OK or DialogResult.Yes ? model : null;
        });
    }

    /// <inheritdoc/>
    public async Task<TViewModel?> ShowStandardAsync<TView, TViewModel>(TViewModel model, OverlayDialogOptions options, CancellationToken cancellationToken = default)
        where TView : UserControl, new()
        where TViewModel : ViewModelBase
    {
        return await dispatcher.DispatchAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            //void ShowStandard<TView, TViewModel>(TViewModel vm, string? hostId = null, OverlayDialogOptions? options = null) where TView : Control, new()
            var dialogResult = await OverlayDialog.ShowStandardAsync<TView, TViewModel>(model, hostId: null, options: options, token: cancellationToken);
            return dialogResult is DialogResult.OK or DialogResult.Yes ? model : null;
        });
    }

    //Task<DialogResult> ShowStandardAsync(Control control, object? vm, string? hostId = null, OverlayDialogOptions? options = null, CancellationToken? token = null)
    //void ShowStandard(object? vm, string? hostId = null, OverlayDialogOptions? options = null)


    /// <inheritdoc/>
    public TViewModel ShowStandard<TView, TViewModel>(TViewModel model, OverlayDialogOptions options, string? overLayHost = null)
        where TView : UserControl, new()
        where TViewModel : ViewModelBase
    {
        dispatcher.Dispatch(() => OverlayDialog.ShowStandard<TView, TViewModel>(model, overLayHost, options: options));
        return model;
    }

    /// <inheritdoc/>
    public void ShowStandard(Control control, object? model, string? hostId = null, OverlayDialogOptions? options = null)
    {
        dispatcher.Dispatch(() =>
            //void ShowStandard(Control control, object? vm, string? hostId = null, OverlayDialogOptions? options = null)
            OverlayDialog.ShowStandard(control, model, hostId, options: options));
    }


    /// <inheritdoc/>
    public async Task<TViewModel> ShowCustomDialogAsync<TView, TViewModel>(TViewModel model, OverlayDialogOptions options, string? overLayHost = null, CancellationToken cancellationToken = default)
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
    public TViewModel ShowCustomDialog<TView, TViewModel>(TViewModel model, OverlayDialogOptions options, string? overLayHost = null)
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
            ProgressDialogViewModel? dialog;
            lock (openProgressDialogsLock)
            {
                dialog = openProgressDialogs.LastOrDefault();
            }
            dialog?.Close();
        });
    }


    /// <inheritdoc/>
    public async Task<bool> ConfirmAsync(OverlayDialogOptions options, string message, CancellationToken cancellationToken = default)
    {
        return await dispatcher.DispatchAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ShowCustomDialogAsync<ConfirmDialogView, ConfirmDialogViewModel>(new ConfirmDialogViewModel(message), options, cancellationToken: cancellationToken);
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
            var result = await ShowCustomDialogAsync<PromptInputDialogView, PromptInputDialogViewModel>(viewModel, options, cancellationToken: cancellationToken);
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
        var result = await ShowCustomDialogAsync<ChoiceDialogView, ChoiceDialogViewModel>(viewModel, options, cancellationToken: cancellationToken);
        return result.Confirmation ? result.SelectedChoice : null;
    }


    /// <inheritdoc/>
    public async Task<int?> PromptAsync(OverlayDialogOptions options, string? message, int initialValue, int minimum, int maximum, CancellationToken cancellationToken = default)
    {
        var viewModel = new PromptIntDialogViewModel(options.Title ?? "", message ?? "", initialValue, minimum, maximum);
        var result = await ShowCustomDialogAsync<PromptIntDialogView, PromptIntDialogViewModel>(viewModel, options, cancellationToken: cancellationToken);
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
        var result = await ShowCustomDialogAsync<PromptIntDialogView, PromptIntDialogViewModel>(viewModel, options, cancellationToken: cancellationToken);
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
        var result = await ShowCustomDialogAsync<PromptDoubleDialogView, PromptDoubleDialogViewModel>(viewModel, options, cancellationToken: cancellationToken);
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
        var result = await ShowCustomDialogAsync<PromptDoubleDialogView, PromptDoubleDialogViewModel>(viewModel, options, cancellationToken: cancellationToken);
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
        ArgumentNullException.ThrowIfNull(options);
        return dispatcher.DispatchAsync<IDisposable>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var model = new ProgressDialogViewModel(message, options.RequestCancellation);
            // Deferred cancellation must keep progress visible until the operation owner
            // disposes the handle, even if the caller's token has already been cancelled.
            var lifetime = options.RequestCancellation is null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : new CancellationTokenSource();
            var overlayOptions = new OverlayDialogOptions
            {
                Title = options.Title,
                FullScreen = false,
                HorizontalAnchor = HorizontalPosition.Center,
                VerticalAnchor = VerticalPosition.Center,
                Buttons = DialogButton.None,
                IsCloseButtonVisible = true,
                CanLightDismiss = false,
                CanDragMove = false,
                CanResize = false,
                TopLevelHashCode = windowProvider.ActiveWindow?.GetHashCode()
            };

            Task<ProgressDialogViewModel> completion;
            try
            {
                completion = ShowCustomDialogAsync<ProgressDialogView, ProgressDialogViewModel>(
                    model, overlayOptions, cancellationToken: lifetime.Token);
                if (completion.IsCompleted)
                {
                    // Report immediate presentation failures to the operation's caller.
                    completion.GetAwaiter().GetResult();
                }
            }
            catch
            {
                lifetime.Dispose();
                model.Dispose();
                throw;
            }

            Register(model);
            // Ursa routes token cancellation through model.Close(), which requests deferred
            // operation cancellation. Owner completion must instead raise RequestClose directly.
            var handle = new DialogHandle(() => dispatcher.Dispatch(model.Complete));
            // ShowCustomDialogAsync completes when the overlay closes. Return its handle
            // immediately so the caller can do the work whose progress it is displaying.
            _ = ObserveProgressDialogAsync(completion, model, lifetime, handle);
            return handle;
        });
    }

    private async Task ObserveProgressDialogAsync(
        Task<ProgressDialogViewModel> completion,
        ProgressDialogViewModel model,
        CancellationTokenSource lifetime,
        IDisposable handle)
    {
        try
        {
            await completion;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
            // Token cancellation and owner disposal both close the overlay normally.
        }
        catch (Exception exception)
        {
            logger?.LogError(exception, "Could not display the progress overlay.");
        }
        finally
        {
            await dispatcher.DispatchAsync(() =>
            {
                handle.Dispose();
                Unregister(model);
                model.Dispose();
                lifetime.Dispose();
            });
        }
    }

    private void Register(ProgressDialogViewModel dialog)
    {
        lock (openProgressDialogsLock)
        {
            openProgressDialogs.Add(dialog);
        }
    }

    private void Unregister(ProgressDialogViewModel dialog)
    {
        lock (openProgressDialogsLock)
        {
            openProgressDialogs.Remove(dialog);
        }
    }

    private sealed class DialogHandle(Action close) : IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            close();
        }
    }
}
