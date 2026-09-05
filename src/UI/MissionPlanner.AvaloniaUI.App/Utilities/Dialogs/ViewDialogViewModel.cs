using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;

/// <summary>
/// 
/// </summary>
public partial class ViewDialogViewModel : ObservableObject
{
    private readonly Action<bool> close;

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

    public bool ShowCloseButton
    {
        get;
        set;
    }

    public bool ShowOkButton
    {
        get;
        set;
    }

    public string Title
    {
        get;
    }

    public Control Content
    {
        get;
    }
    public string OkText
    {
        get;
    }
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
