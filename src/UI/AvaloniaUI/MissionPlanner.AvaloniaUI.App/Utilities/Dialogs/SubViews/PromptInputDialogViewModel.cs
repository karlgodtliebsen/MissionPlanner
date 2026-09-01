using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.AvaloniaUI.App.Utilities.Dialogs.SubViews;

public partial class PromptInputDialogViewModel : ObservableObject
{

    /// <inheritdoc />
    public PromptInputDialogViewModel(string? initialValue, string? message)
    {
        PromptText = initialValue;
        Message = message;
    }

    [ObservableProperty]
    public partial string? PromptText
    {
        get;
        set;
    }
    [ObservableProperty]
    public partial string? Message
    {
        get;
        set;
    }

}
