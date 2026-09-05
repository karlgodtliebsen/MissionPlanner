using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.App.Utilities.Dialogs.SubViews;

public partial class PromptDoubleDialogViewModel : DialogViewModelBase
{

    /// <inheritdoc />
    public PromptDoubleDialogViewModel(string title, string message, double? initialValue, double? minimum = null, double? maximum = null)
    {
        Value = initialValue;
        Title = title;
        Max = maximum ?? double.MaxValue;
        Min = minimum ?? double.MinValue;
        Message = message;
    }


    /// <inheritdoc/>
    public override void Cancel()
    {
        Value = null;
        base.Cancel();
    }

    [ObservableProperty]
    public partial double? Value
    {
        get;
        set;
    }

    public double Min
    {
        get;
        set;
    }

    public double Max
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
