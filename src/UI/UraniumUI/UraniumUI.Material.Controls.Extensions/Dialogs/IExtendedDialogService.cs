using UraniumUI.Dialogs;

namespace UraniumUI.Material.Dialogs;

/// <summary>
/// Provides extended dialog services.
/// </summary>
public interface IExtendedDialogService : IDialogService
{
    /// <summary>
    /// Displays a cancellable progress dialog.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="message">A function that returns the message to display.</param>
    /// <param name="cancelText">The text for the cancel button.</param>
    /// <param name="tokenSource">The cancellation token source.</param>
    /// <returns>A disposable object that can be used to close the dialog.</returns>
    Task<IDisposable> DisplayProgressCancellableAsync(string title, Func<string> message, string cancelText = "Cancel", CancellationTokenSource? tokenSource = default);


    /// <summary>
    /// Displays a custom view dialog.
    /// </summary>
    /// <param name="page">The page on which to display the dialog.</param>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="content">The content view to display.</param>
    /// <param name="options"></param>
    /// <param name="okText">The text for the OK button.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A disposable object that can be used to close the dialog.</returns>
    Task<bool> DisplayViewAsync(Page page, string title, View content, ViewDialogOptions? options = null, string okText = "OK", CancellationToken cancellationToken = default);
}
