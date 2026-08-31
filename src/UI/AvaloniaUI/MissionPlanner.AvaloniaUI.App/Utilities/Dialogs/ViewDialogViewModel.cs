using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;

/// <summary>
/// 
/// </summary>
public sealed partial class ViewDialogViewModel : ObservableObject
{
    private readonly Action close;

    public ViewDialogViewModel(string title, Control content, string closeText, Action close)
    {
        TitleText = title;
        DialogContent = content;
        CloseText = closeText;
        this.close = close;
    }

    public string TitleText
    {
        get;
    }

    public Control DialogContent
    {
        get;
    }

    public string CloseText
    {
        get;
    }

    [RelayCommand]
    private void Close()
    {
        close();
    }
}
