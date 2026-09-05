
using Avalonia.Controls;
using Ursa.Controls;

namespace MissionPlanner.App.Utilities.Dialogs;

/// <summary>
/// Provides extended dialog services.
/// Defines methods for navigating modally within the application.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Creates overlay dialog options with the specified title, accept text, and cancel text.
    /// </summary>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="accept">The text for the accept button.</param>
    /// <param name="cancel">The text for the cancel button.</param>
    /// <returns>The created overlay dialog options.</returns>
    OverlayDialogOptions CreateOptions(string title, string? accept = null, string? cancel = null);

    /// <summary>
    /// Displays a confirmation dialog with the specified message and options.
    /// </summary>
    /// <param name="options">The dialog options.</param>
    /// <param name="message">The message to display in the confirmation dialog.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if the user confirmed; otherwise, false.</returns>
    Task<bool> ConfirmAsync(OverlayDialogOptions options, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Displays a choice dialog with the specified options and choices.
    /// </summary>
    /// <param name="options">The overlay dialog options.</param>
    /// <param name="choices">The list of choices to display in the dialog.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the selected choice or null if canceled.</returns>
    Task<string?> ChooseAsync(OverlayDialogOptions options, IReadOnlyList<string> choices, CancellationToken cancellationToken = default);

    /// <summary>
    /// Displays an overlay dialog with the specified view model and options.
    /// </summary>
    /// <param name="model">The view model for the dialog.</param>
    /// <param name="options">The overlay dialog options.</param>
    /// <param name="overLayHost">The optional overlay host.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <typeparam name="TView">The type of the view.</typeparam>
    /// <typeparam name="TViewModel">The type of the view model.</typeparam>
    /// <returns>A task that represents the asynchronous operation. The task result contains the view model.</returns>
    Task<TViewModel> ShowOverlayDialogAsync<TView, TViewModel>(TViewModel model, OverlayDialogOptions options, string? overLayHost = null, CancellationToken cancellationToken = default)
        where TView : UserControl, new()
        where TViewModel : DialogViewModelBase;

    /// <summary>
    /// Displays an overlay dialog with the specified view model and options.
    /// </summary>
    /// <param name="model">The view model for the dialog.</param>
    /// <param name="options">The overlay dialog options.</param>
    /// <param name="overLayHost">The optional overlay host.</param>
    /// <typeparam name="TView">The type of the view.</typeparam>
    /// <typeparam name="TViewModel">The type of the view model.</typeparam>
    /// <returns>The view model.</returns>
    TViewModel ShowOverlayDialog<TView, TViewModel>(TViewModel model, OverlayDialogOptions options, string? overLayHost = null)
        where TView : UserControl, new()
        where TViewModel : DialogViewModelBase;

    /// <summary>
    /// Displays a prompt dialog with the specified options, message, and initial value.
    /// </summary>
    /// <param name="options">The overlay dialog options.</param>
    /// <param name="message">The message to display in the prompt dialog.</param>
    /// <param name="initialValue">The initial value for the prompt input.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entered string or null if canceled.</returns>
    Task<string?> PromptAsync(OverlayDialogOptions options, string? message, string? initialValue = null, CancellationToken cancellationToken = default);


    /// <summary>
    /// Displays a prompt dialog with the specified title, message, and initial value.
    /// </summary>
    /// <param name="title">The title of the prompt dialog.</param>
    /// <param name="message">The message to display in the prompt dialog.</param>
    /// <param name="initialValue">The initial value for the prompt input.</param>
    /// <param name="accept">The text for the accept button.</param>
    /// <param name="cancel">The text for the cancel button.</param>
    /// <param name="clear">The text for the clear button.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entered string or null if canceled.</returns>
    Task<string?> PromptAsync(string title, string message, string initialValue, string accept = "OK", string cancel = "Cancel", string clear = "Clear", CancellationToken cancellationToken = default);

    /// <summary>
    /// Displays a prompt dialog with the specified overlay dialog options, message, initial value, minimum, and maximum.
    /// </summary>
    /// <param name="options">The overlay dialog options.</param>
    /// <param name="message">The message to display in the prompt dialog.</param>
    /// <param name="initialValue">The initial value for the prompt input.</param>
    /// <param name="minimum">The minimum value for the prompt input.</param>
    /// <param name="maximum">The maximum value for the prompt input.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entered integer or null if canceled.</returns>
    Task<int?> PromptAsync(OverlayDialogOptions options, string? message, int initialValue, int minimum, int maximum, CancellationToken cancellationToken = default);

    /// <summary>
    /// Displays a prompt dialog with the specified title, message, initial value, minimum, and maximum.
    /// </summary>
    /// <param name="title">The title of the prompt dialog.</param>
    /// <param name="message">The message to display in the prompt dialog.</param>
    /// <param name="initialValue">The initial value for the prompt input.</param>
    /// <param name="minimum">The minimum value for the prompt input.</param>
    /// <param name="maximum">The maximum value for the prompt input.</param>
    /// <param name="accept">The text for the accept button.</param>
    /// <param name="cancel">The text for the cancel button.</param>
    /// <param name="clear">The text for the clear button.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entered integer or null if canceled.</returns>
    Task<int?> PromptAsync(string title, string message, int initialValue, int minimum, int maximum, string accept = "OK", string cancel = "Cancel", string clear = "Clear", CancellationToken cancellationToken = default);

    /// <summary>
    /// Displays a prompt dialog with the specified title, message, initial value, minimum, and maximum.
    /// </summary>
    /// <param name="title">The title of the prompt dialog.</param>
    /// <param name="message">The message to display in the prompt dialog.</param>
    /// <param name="initialValue">The initial value for the prompt input.</param>
    /// <param name="minimum">The minimum value for the prompt input.</param>
    /// <param name="maximum">The maximum value for the prompt input.</param>
    /// <param name="accept">The text for the accept button.</param>
    /// <param name="cancel">The text for the cancel button.</param>
    /// <param name="clear">The text for the clear button.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entered double or null if canceled.</returns>
    Task<double?> PromptAsync(string title, string message, double initialValue, double? minimum = null, double? maximum = null, string accept = "OK", string cancel = "Cancel", string clear = "Clear", CancellationToken cancellationToken = default);

    /// <summary>
    /// Displays a prompt dialog with the specified overlay dialog options, message, initial value, minimum, and maximum.
    /// </summary>
    /// <param name="options">The overlay dialog options.</param>
    /// <param name="message">The message to display in the prompt dialog.</param>
    /// <param name="initialValue">The initial value for the prompt input.</param>
    /// <param name="minimum">The minimum value for the prompt input.</param>
    /// <param name="maximum">The maximum value for the prompt input.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the entered double or null if canceled.</returns>
    Task<double?> PromptAsync(OverlayDialogOptions options, string? message, double initialValue, double? minimum = null, double? maximum = null, CancellationToken cancellationToken = default);


    /// <summary>
    /// Displays a cancellable progress dialog.
    /// </summary>
    /// <param name="message">A function that returns the message to display in the progress dialog.</param>
    /// <param name="options">The dialog options.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A disposable object that can be used to close the progress dialog.</returns>   
    Task<IDisposable> DisplayProgressCancellableAsync(Func<string> message, DialogOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the most recently opened window dialog.
    /// </summary>
    Task CloseAsync(CancellationToken cancellationToken = default);
}
