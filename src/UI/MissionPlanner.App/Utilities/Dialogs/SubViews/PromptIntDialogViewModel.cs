using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.App.Utilities.Dialogs.SubViews;

public partial class PromptIntDialogViewModel : DialogViewModelBase
{

    /// <inheritdoc />
    public PromptIntDialogViewModel(string title, string message, int? initialValue, int? minimum = null, int? maximum = null)
    {
        Value = initialValue;
        Title = title;
        Max = maximum ?? int.MaxValue;
        Min = minimum ?? int.MinValue;
        Message = message;
    }


    /// <inheritdoc/>
    public override void Cancel()
    {
        Value = null;
        base.Cancel();
    }

    [ObservableProperty]
    public partial int? Value
    {
        get;
        set;
    }

    public double Min
    {
        get;
        set;
    }

    public int Max
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
