using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlanner.App.Utilities.Dialogs;

/// <summary>
/// 
/// </summary>
public partial class ViewDialogViewModel : ObservableObject
{
    private readonly Action<bool> close;

    /// <summary>Creates the view model for the content, title and buttons of a dialog window.</summary>
    public ViewDialogViewModel(string title, Control content, string okText, string closeText, bool showOkButton, bool showCloseButton, Action<bool> close)
    {
        Title = title;
        Content = content;
        OkText = okText;
        CloseText = closeText;
        ShowOkButton = showOkButton;
        ShowCloseButton = showCloseButton;
        this.close = close;
    }

    /// <summary>Gets whether the Close button is shown.</summary>
    public bool ShowCloseButton
    {
        get;
        set;
    }

    /// <summary>Gets whether the OK button is shown.</summary>
    public bool ShowOkButton
    {
        get;
        set;
    }

    /// <summary>Gets the dialog title.</summary>
    public string Title
    {
        get;
    }

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


    [RelayCommand]
    private void Ok()
    {
        close(true);
    }


    [RelayCommand]
    private void Cancel()
    {
        close(false);
    }
}
