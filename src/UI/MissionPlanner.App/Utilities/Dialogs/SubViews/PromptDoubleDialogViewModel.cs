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

    /// <summary>Gets or sets the lowest accepted value.</summary>
    public double Min
    {
        get;
        set;
    }

    /// <summary>Gets or sets the highest accepted value.</summary>
    public double Max
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
