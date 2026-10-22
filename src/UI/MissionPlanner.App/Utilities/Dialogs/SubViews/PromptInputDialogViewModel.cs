using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.App.Utilities.Dialogs.SubViews;

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


    /// <summary>Gets or sets the message shown above the input.</summary>
    public string? Message
    {
        get;
        set;
    }
}
