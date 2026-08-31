using Avalonia.Controls;
using MissionPlanner.AvaloniaUI.App.Utilities.Dispatching;

namespace MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;

public sealed class AvaloniaDialogService : IDialogService
{
    private readonly IUiDispatcher uiDispatcher;
    private readonly IWindowProvider windowProvider;

    public AvaloniaDialogService(IUiDispatcher uiDispatcher, IWindowProvider windowProvider)
    {
        this.uiDispatcher = uiDispatcher;
        this.windowProvider = windowProvider;
    }

    public async Task DisplayViewAsync(string title, Control content, string closeText)
    {
        await uiDispatcher.DispatchAsync(async () =>
        {
            var owner = windowProvider.ActiveWindow
                        ?? throw new InvalidOperationException(
                            "No active window is available.");

            var dialog = new ViewDialogWindow
            {
                Title = title,
                Width = 800,
                Height = 600
            };

            dialog.DataContext =
                new ViewDialogViewModel(
                    title,
                    content,
                    closeText,
                    dialog.Close);

            await dialog.ShowDialog(owner);
        });
    }

    /// <inheritdoc />
    public async Task DisplayViewAsync(string title, UserControl content, string okText = "OK")
    {
        var closeText = "Close";
        await uiDispatcher.DispatchAsync(async () =>
        {
            var owner = windowProvider.ActiveWindow
                        ?? throw new InvalidOperationException(
                            "No active window is available.");

            var dialog = new ViewDialogWindow
            {
                Title = title,
                Width = 800,
                Height = 600
            };

            dialog.DataContext =
                new ViewDialogViewModel(
                    title,
                    content,
                    closeText,
                    dialog.Close);

            await dialog.ShowDialog(owner);
        });
    }

    /// <inheritdoc />
    public Task<bool> DisplayViewAsync(string title, UserControl content, string okText, string cancelText)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<IDisposable> DisplayProgressAsync(string title, string message)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<IDisposable> DisplayProgressCancellableAsync(string title, string message, string cancelText = "Cancel", CancellationTokenSource? tokenSource = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string title, string message, string okText = "OK", string cancelText = "Cancel")
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<IEnumerable<T>> DisplayCheckBoxPromptAsync<T>(string message, IEnumerable<T> selectionSource, IEnumerable<T>? selectedItems = default, string accept = "OK", string cancel = "Cancel", string? displayMember = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<T> DisplayRadioButtonPromptAsync<T>(string message, IEnumerable<T> selectionSource, T selected = default(T), string accept = "Ok", string cancel = "Cancel", string? displayMember = null)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<string> DisplayTextPromptAsync(string title, string message, string accept = "OK", string cancel = "Cancel", string? placeholder = null, int maxLength = -1, string initialValue = "", bool isPassword = false)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<DateTime?> DisplayDatePromptAsync(string title, DateTime? selectedDate = null, DateTime? minimumDate = null, DateTime? maximumDate = null, string accept = "OK", string cancel = "Cancel", string clear = "Clear", string today = "Today")
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<TViewModel> DisplayFormViewAsync<TViewModel>(string title, TViewModel? viewModel = default, string submit = "OK", string cancel = "Cancel") where TViewModel : class
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
    public Task ShowAsync<TPage>(bool animated = true, CancellationToken cancellationToken = default) where TPage : Page
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task ShowAsync(Page page, bool animated = true, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task CloseAsync(bool animated = true, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task DisplayViewExtendedAsync(string title, UserControl content, string okText = "OK")
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<bool> DisplayViewExtendedAsync(string title, UserControl content, string okText, string cancelText)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<bool> DisplayViewExtendedAsync(Page page, string title, UserControl content, ViewDialogOptions? options = null, string okText = "OK", CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public Task<IDisposable> DisplayProgressCancellableAsync(string title, Func<string> message, string cancelText = "Cancel", CancellationTokenSource? tokenSource = default)
    {
        throw new NotImplementedException();
    }
}
