using UraniumUI.Dialogs;

namespace UraniumUI.Material.Dialogs;

/// <summary>
/// Provides extended dialog services.
/// Defines methods for navigating modally within the application.
/// </summary>
public interface IExtendedDialogService : IDialogService
{
    /// <summary>
    /// Displays a modal page of the specified type.
    /// </summary>
    /// <param name="animated">Indicates whether the display should be animated.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <typeparam name="TPage">The type of the page to display.</typeparam>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ShowAsync<TPage>(bool animated = true, CancellationToken cancellationToken = default) where TPage : Page;


    /// <summary>
    /// Displays a modal page.
    /// </summary>
    /// <param name="page">The page to display.</param>
    /// <param name="animated">Indicates whether the display should be animated.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ShowAsync(Page page, bool animated = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the currently displayed modal page.
    /// </summary>
    /// <param name="animated">Indicates whether the closing should be animated.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CloseAsync(bool animated = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Displays a view in a lightweight modal dialog with a resilient,
    /// command-detached close operation.
    /// </summary>
    Task DisplayViewExtendedAsync(string title, View content, string okText = "OK");

    /// <summary>
    /// Displays a view in a lightweight modal dialog and returns whether the
    /// user accepted it. Closing is detached from the bound button command.
    /// </summary>
    Task<bool> DisplayViewExtendedAsync(string title, View content, string okText, string cancelText);

    /// <summary>
    /// Displays a custom view dialog with a customizable size.
    /// Uses the provided <paramref name="page"/> to display the dialog.
    /// </summary>
    /// <param name="page">The page on which to display the dialog.</param>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="content">The content view to display.</param>
    /// <param name="options"></param>
    /// <param name="okText">The text for the OK button.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A disposable object that can be used to close the dialog.</returns>
    Task<bool> DisplayViewExtendedAsync(Page page, string title, View content, ViewDialogOptions? options = null, string okText = "OK", CancellationToken cancellationToken = default);

    /// <summary>
    /// Displays a cancellable progress dialog.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">A function that returns the message to display.</param>
    /// <param name="cancelText">The text for the cancel button.</param>
    /// <param name="tokenSource">The cancellation token source.</param>
    /// <returns>A disposable object that can be used to close the dialog.</returns>
    Task<IDisposable> DisplayProgressCancellableAsync(string title, Func<string> message, string cancelText = "Cancel", CancellationTokenSource? tokenSource = default);
}
