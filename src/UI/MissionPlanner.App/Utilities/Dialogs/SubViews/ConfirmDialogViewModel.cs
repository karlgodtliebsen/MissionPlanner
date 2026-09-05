namespace MissionPlanner.App.Utilities.Dialogs.SubViews;

/// <summary>
/// ViewModel for a confirmation dialog.
/// </summary>
public partial class ConfirmDialogViewModel : DialogViewModelBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmDialogViewModel"/> class.
    /// </summary>
    /// <param name="message">The message to display in the confirmation dialog.</param>
    public ConfirmDialogViewModel(string message)
    {
        Message = message;
    }

    /// <inheritdoc/>
    public string? Message
    {
        get; set;
    }
}
