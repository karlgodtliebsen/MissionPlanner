
using Avalonia.Controls;

namespace MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;

/// <summary>
/// Provides extended dialog services.
/// Defines methods for navigating modally within the application.
/// </summary>
public interface IDialogService
{

    /// <summary>
    /// 
    /// </summary>
    /// <param name="title"></param>
    /// <param name="content"></param>
    /// <param name="okText"></param>
    /// <returns></returns>
    Task DisplayViewAsync(string title, UserControl content, string okText = "OK");

    /// <summary>
    /// 
    /// </summary>
    /// <param name="title"></param>
    /// <param name="content"></param>
    /// <param name="okText"></param>
    /// <param name="cancelText"></param>
    /// <returns></returns>
    Task<bool> DisplayViewAsync(string title, UserControl content, string okText, string cancelText);


    /// <summary>
    /// Displays a progress dialog.
    /// </summary>
    /// <param name="title">The title of the progress dialog.</param>
    /// <param name="message">The message to display in the progress dialog.</param>
    /// <returns>A disposable object that can be used to close the progress dialog.</returns>
    Task<IDisposable> DisplayProgressAsync(string title, string message);

    /// <summary>
    /// Displays a cancellable progress dialog.
    /// </summary>
    /// <param name="title">The title of the progress dialog.</param>
    /// <param name="message">The message to display in the progress dialog.</param>
    /// <param name="cancelText">The text to display on the cancel button.</param>
    /// <param name="tokenSource">The cancellation token source to cancel the operation.</param>
    /// <returns>A disposable object that can be used to close the progress dialog.</returns>   
    Task<IDisposable> DisplayProgressCancellableAsync(
        string title,
        string message,
        string cancelText = "Cancel",
        CancellationTokenSource? tokenSource = default);

    /// <summary>
    /// Displays a confirmation dialog.
    /// </summary>
    /// <param name="title">The title of the confirmation dialog.</param>
    /// <param name="message">The message to display in the confirmation dialog.</param>
    /// <param name="okText">The text to display on the OK button.</param>
    /// <param name="cancelText">The text to display on the Cancel button.</param>
    /// <returns></returns>
    Task<bool> ConfirmAsync(
        string title,
        string message,
        string okText = "OK",
        string cancelText = "Cancel");

    /// <summary>
    /// Displays a checkbox prompt dialog.
    /// </summary>
    /// <param name="message">The message to display in the prompt.</param>
    /// <param name="selectionSource">The collection of items to display as checkboxes.</param>
    /// <param name="selectedItems">The collection of items that are initially selected.</param>
    /// <param name="accept">The text to display on the accept button.</param>
    /// <param name="cancel">The text to display on the cancel button.</param>
    /// <param name="displayMember">The name of the property to display for each item.</param>
    /// <typeparam name="T">The type of the items in the selection source.</typeparam>
    /// <returns>The collection of items that were selected.</returns>
    Task<IEnumerable<T>> DisplayCheckBoxPromptAsync<T>(
        string message,
        IEnumerable<T> selectionSource,
        IEnumerable<T>? selectedItems = default,
        string accept = "OK",
        string cancel = "Cancel",
        string? displayMember = null);

    /// <summary>
    /// Displays a radio button prompt dialog.
    /// </summary>
    /// <param name="message">The message to display in the prompt.</param>
    /// <param name="selectionSource">The collection of items to display as radio buttons.</param>
    /// <param name="selected">The item that is initially selected.</param>
    /// <param name="accept">The text to display on the accept button.</param>
    /// <param name="cancel">The text to display on the cancel button.</param>
    /// <param name="displayMember">The name of the property to display for each item.</param>
    /// <typeparam name="T">The type of the items in the selection source.</typeparam>
    /// <returns>The item that was selected.</returns>
    Task<T> DisplayRadioButtonPromptAsync<T>(
        string message,
        IEnumerable<T> selectionSource,
        T selected = default(T),
        string accept = "Ok",
        string cancel = "Cancel", string? displayMember = null);

    /// <summary>
    /// Displays a text prompt dialog.
    /// </summary>
    /// <param name="title">The title of the text prompt dialog.</param>
    /// <param name="message">The message to display in the text prompt dialog.</param>
    /// <param name="accept">The text to display on the accept button.</param>
    /// <param name="cancel">The text to display on the cancel button.</param>
    /// <param name="placeholder">The placeholder text to display in the input field.</param>
    /// <param name="maxLength">The maximum length of the input text.</param>
    /// <param name="initialValue">The initial value to display in the input field.</param>
    /// <param name="isPassword">Whether the input field should mask the entered text as a password.</param>
    /// <returns>The text entered by the user. </returns>
    Task<string> DisplayTextPromptAsync(
        string title,
        string message,
        string accept = "OK",
        string cancel = "Cancel",
        string? placeholder = null,
        int maxLength = -1,
        string initialValue = "",
        bool isPassword = false);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="title"></param>
    /// <param name="selectedDate"></param>
    /// <param name="minimumDate"></param>
    /// <param name="maximumDate"></param>
    /// <param name="accept"></param>
    /// <param name="cancel"></param>
    /// <param name="clear"></param>
    /// <param name="today"></param>
    /// <returns></returns>
    Task<DateTime?> DisplayDatePromptAsync(
        string title,
        DateTime? selectedDate = null,
        DateTime? minimumDate = null,
        DateTime? maximumDate = null,
        string accept = "OK",
        string cancel = "Cancel",
        string clear = "Clear",
        string today = "Today");

    /// <summary>
    /// 
    /// </summary>
    /// <param name="title"></param>
    /// <param name="viewModel"></param>
    /// <param name="submit"></param>
    /// <param name="cancel"></param>
    /// <typeparam name="TViewModel"></typeparam>
    /// <returns></returns>
    Task<TViewModel> DisplayFormViewAsync<TViewModel>(
        string title,
        TViewModel? viewModel = default,
        string submit = "OK",
        string cancel = "Cancel") where TViewModel : class;


    /// <summary>
    /// Displays a right-aligned multiline text prompt.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="message">Instruction displayed above the input field.</param>
    /// <param name="initialValue">Initial text displayed in the editor.</param>
    /// <param name="accept">Accept button text.</param>
    /// <param name="cancel">Cancel button text.</param>
    /// <param name="clear">Clear button text; empty hides the button.</param>
    /// <returns>The edited text, or <see langword="null"/> on Cancel or Clear.</returns>
    Task<string?> DisplayPromptAsync(
        string title,
        string message,
        string initialValue,
        string accept = "OK",
        string cancel = "Cancel",
        string clear = "Clear");

    /// <summary>
    /// Displays a prompt dialog for entering a TimeSpan value. 
    /// </summary>
    /// <param name="title">The title of the prompt dialog.</param>
    /// <param name="message">The message to display in the prompt dialog.</param>
    /// <param name="initialValue">The initial value to display in the input field.</param>
    /// <param name="minimumTime">The minimum allowable TimeSpan value.</param>
    /// <param name="maximumTime">The maximum allowable TimeSpan value.</param>
    /// <param name="accept">The text to display on the accept button.</param>
    /// <param name="cancel">The text to display on the cancel button.</param>
    /// <param name="clear"></param>
    /// <returns></returns>
    Task<TimeSpan?> DisplayPromptAsync(
        string title,
        string message,
        TimeSpan? initialValue = null,
        TimeSpan? minimumTime = null,
        TimeSpan? maximumTime = null,
        string accept = "OK",
        string cancel = "Cancel",
        string clear = "Clear");

    /// <summary>
    /// Displays a prompt dialog for entering a double value. 
    /// </summary>
    /// <param name="title"></param>
    /// <param name="message"></param>
    /// <param name="initialValue"></param>
    /// <param name="minimum"></param>
    /// <param name="maximum"></param>
    /// <param name="accept"></param>
    /// <param name="cancel"></param>
    /// <param name="clear"></param>
    /// <returns></returns>
    Task<int?> DisplayPromptAsync(
        string title,
        string message,
        int? initialValue = null,
        int? minimum = null,
        int? maximum = null,
        string accept = "OK",
        string cancel = "Cancel",
        string clear = "Clear");

    /// <summary>
    /// Displays a prompt dialog for entering a long value. 
    /// </summary>
    /// <param name="title"></param>
    /// <param name="message"></param>
    /// <param name="initialValue"></param>
    /// <param name="minimum"></param>
    /// <param name="maximum"></param>
    /// <param name="accept"></param>
    /// <param name="cancel"></param>
    /// <param name="clear"></param>
    /// <returns></returns>
    Task<long?> DisplayPromptAsync(
        string title,
        string message,
        long? initialValue = null,
        long? minimum = null,
        long? maximum = null,
        string accept = "OK",
        string cancel = "Cancel",
        string clear = "Clear");

    /// <summary>
    /// Displays a prompt dialog for entering a float value. 
    /// </summary>
    /// <param name="title"></param>
    /// <param name="message"></param>
    /// <param name="initialValue"></param>
    /// <param name="minimum"></param>
    /// <param name="maximum"></param>
    /// <param name="accept"></param>
    /// <param name="cancel"></param>
    /// <param name="clear"></param>
    /// <returns></returns>
    Task<float?> DisplayPromptAsync(
        string title,
        string message,
        float? initialValue = null,
        float? minimum = null,
        float? maximum = null,
        string accept = "OK",
        string cancel = "Cancel",
        string clear = "Clear");

    /// <summary>
    /// Displays a prompt dialog for entering a double value. 
    /// </summary>
    /// <param name="title"></param>
    /// <param name="message"></param>
    /// <param name="initialValue"></param>
    /// <param name="minimum"></param>
    /// <param name="maximum"></param>
    /// <param name="accept"></param>
    /// <param name="cancel"></param>
    /// <param name="clear"></param>
    /// <returns></returns>
    Task<double?> DisplayPromptAsync(
        string title,
        string message,
        double? initialValue = null, double? minimum = null, double? maximum = null,
        string accept = "OK", string cancel = "Cancel",
        string clear = "Clear");

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
    Task DisplayViewExtendedAsync(string title, UserControl content, string okText = "OK");

    /// <summary>
    /// Displays a view in a lightweight modal dialog and returns whether the
    /// user accepted it. Closing is detached from the bound button command.
    /// </summary>
    Task<bool> DisplayViewExtendedAsync(string title, UserControl content, string okText, string cancelText);

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
    Task<bool> DisplayViewExtendedAsync(Page page, string title, UserControl content, ViewDialogOptions? options = null, string okText = "OK", CancellationToken cancellationToken = default);

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
