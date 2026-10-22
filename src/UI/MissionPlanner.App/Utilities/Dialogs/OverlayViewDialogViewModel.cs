using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace MissionPlanner.App.Utilities.Dialogs;

public sealed partial class OverlayViewDialogViewModel : ObservableObject, IDialogContext
{
    /// <summary>Creates the view model for the content, title and buttons of an overlay dialog.</summary>
    public OverlayViewDialogViewModel(
        string title,
        Control content,
        string okText,
        string closeText,
        bool showOkButton,
        bool showCloseButton)
    {
        Title = title;
        Content = content;
        OkText = okText;
        CloseText = closeText;
        ShowOkButton = showOkButton;
        ShowCloseButton = showCloseButton;
    }

    /// <summary>Gets the dialog title.</summary>
    public string Title
    {
        get;
        set;
    } = "The Title";

    /// <summary>Gets the content shown in the dialog.</summary>
    public Control Content
    {
        get;
    }

    /// <summary>Gets the text of the OK button.</summary>
    public string OkText
    {
        get;
    }

    /// <summary>Gets the text of the Close button.</summary>
    public string CloseText
    {
        get;
    }

    /// <summary>Gets whether the OK button is shown.</summary>
    public bool ShowOkButton
    {
        get;
    }

    /// <summary>Gets whether the Close button is shown.</summary>
    public bool ShowCloseButton
    {
        get;
    }


    /// <summary>Occurs when the dialog asks to be closed.</summary>
    public event EventHandler<object?>? RequestClose;


    /// <summary>Closes the dialog without confirming.</summary>
    public void Close()
    {
        RequestClose?.Invoke(this, false);
    }


    [RelayCommand]
    private void Ok()
    {
        RequestClose?.Invoke(this, true);
    }


    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
    }
}
