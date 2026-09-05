using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.AvaloniaUI.App.Utilities.Dialogs.SubViews;

public partial class PromptInputDialogViewModel : DialogViewModelBase
{

    /// <inheritdoc />
    public PromptInputDialogViewModel(string? initialValue, string? message)
    {
        PromptText = initialValue;
        Message = message;
    }


    /// <inheritdoc/>
    public override void Cancel()
    {
        PromptText = null;
        base.Cancel();
    }

    [ObservableProperty]
    public partial string? PromptText
    {
        get;
        set;
    }


    public string? Message
    {
        get;
        set;
    }
}
