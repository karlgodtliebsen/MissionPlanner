using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;

namespace MissionPlanner.App.Utilities.Dialogs;

public sealed partial class OverlayViewDialogViewModel : ObservableObject, IDialogContext
{
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

    public bool ShowOkButton
    {
        get;
    }

    public bool ShowCloseButton
    {
        get;
    }


    public event EventHandler<object?>? RequestClose;


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
