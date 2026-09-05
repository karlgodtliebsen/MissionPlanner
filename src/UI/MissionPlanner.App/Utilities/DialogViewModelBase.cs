using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace MissionPlanner.App.Utilities;

/// <summary>
/// Base class for dialog view models, providing common functionality for closing and confirming dialogs.
/// </summary>
public partial class DialogViewModelBase : ViewModelBase, IDialogContext
{
    public string? Title
    {
        get;
        set;
    }

    public string? OkText
    {
        get;
        set;
    }

    public string? CancelText
    {
        get;
        set;
    }


    /// <inheritdoc/>
    public bool Confirmation
    {
        get; set;
    }
    public bool Closed
    {
        get; set;
    }


    /// <summary>
    /// Closes the dialog.
    /// </summary>
    public virtual void Close()
    {
        Closed = true;
        RequestClose?.Invoke(this, null);
    }

    /// <summary>
    /// Confirms the dialog.
    /// </summary>
    [RelayCommand]
    public virtual void OK()
    {
        Confirmation = true;
        RequestClose?.Invoke(this, true);
    }

    /// <summary>
    /// Cancels the dialog.
    /// </summary>
    [RelayCommand]
    public virtual void Cancel()
    {
        Confirmation = false;
        RequestClose?.Invoke(this, false);
    }


    /// <summary>
    /// Occurs when a request to close the dialog is made.
    /// The event argument indicates whether the dialog was confirmed (true) or canceled (false).
    /// </summary>
    public event EventHandler<object?>? RequestClose;

}
