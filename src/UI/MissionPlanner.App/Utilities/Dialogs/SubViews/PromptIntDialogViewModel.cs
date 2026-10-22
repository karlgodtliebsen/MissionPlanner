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

    /// <summary>Gets or sets the lowest accepted value.</summary>
    public double Min
    {
        get;
        set;
    }

    /// <summary>Gets or sets the highest accepted value.</summary>
    public int Max
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
