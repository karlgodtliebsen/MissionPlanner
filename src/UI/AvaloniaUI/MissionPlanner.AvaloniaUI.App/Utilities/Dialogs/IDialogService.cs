
using Avalonia.Controls;

namespace MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;

/// <summary>
/// Provides extended dialog services.
/// Defines methods for navigating modally within the application.
/// </summary>
public interface IDialogService
{

    /// <summary>
    /// Displays a confirmation dialog with the specified message and options.
    /// </summary>
    /// <param name="message">The message to display in the confirmation dialog.</param>
    /// <param name="options">The dialog options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains true if the user confirmed; otherwise, false.</returns>
    Task<bool> ConfirmAsync(string message, DialogOptions options, CancellationToken cancellationToken = default);


    Task<string?> ChooseAsync(DialogOptions options, IReadOnlyList<string> choices, CancellationToken cancellationToken = default);

    //Task<bool> PromptAsync(Control content, DialogOptions options, CancellationToken cancellationToken = default);

    //Task<string?> PromptAsync(DialogOptions options, string message, Control editor, Func<string?> result, CancellationToken cancellationToken = default);

    Task<string?> PromptAsync(DialogOptions options, string? message, string? initialValue = null, CancellationToken cancellationToken = default);


    Task<bool> ShowWindowAsync(Control content, DialogOptions options, CancellationToken cancellationToken = default);

    Task<bool> ShowOverlayAsync(Control content, DialogOptions options, CancellationToken cancellationToken = default);

    /// <summary>Closes the most recently opened window dialog.</summary>
    Task CloseAsync(CancellationToken cancellationToken = default);

    Task<string?> DisplayPromptAsync(string title, string message, string initialValue,
        string accept = "OK", string cancel = "Cancel", string clear = "Clear");

    Task<int?> PromptAsync(DialogOptions options, string? message, int initialValue, int minimum, int maximum,
        CancellationToken cancellationToken = default);

    Task<int?> DisplayPromptAsync(string title, string message, int initialValue, int minimum, int maximum,
        string accept = "OK", string cancel = "Cancel", string clear = "Clear");

    Task<double?> DisplayPromptAsync(string title, string message, double initialValue,
        double? minimum = null, double? maximum = null, string accept = "OK", string cancel = "Cancel", string clear = "Clear");

    /// <summary>
    /// Displays a cancellable progress dialog.
    /// </summary>
    /// <param name="message">A function that returns the message to display in the progress dialog.</param>
    /// <param name="options">The dialog options.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A disposable object that can be used to close the progress dialog.</returns>   
    Task<IDisposable> DisplayProgressCancellableAsync(Func<string> message, DialogOptions options, CancellationToken cancellationToken = default);
}
