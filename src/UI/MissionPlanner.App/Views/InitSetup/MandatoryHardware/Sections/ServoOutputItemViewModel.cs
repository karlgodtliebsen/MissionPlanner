using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.Setup;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Presents one servo output with a function picker and live PWM.</summary>
public sealed partial class ServoOutputItemViewModel : ObservableObject
{
    private readonly Action<ServoOutputItemViewModel> isDirtyAction;
    private readonly bool suppressApply = true;

    /// <summary>
    /// Gets a value indicating whether the servo output has been modified.
    /// </summary>
    [ObservableProperty]
    public partial bool IsDirty { get; private set; }


    /// <summary>Initializes a servo output row.</summary>
    /// <param name="info">The output projection.</param>
    /// <param name="options">The available functions.</param>
    /// <param name="isDirtyAction"></param>
    public ServoOutputItemViewModel(ServoOutputInfo info, IReadOnlyList<ServoFunctionOption> options, Action<ServoOutputItemViewModel> isDirtyAction)
    {
        this.isDirtyAction = isDirtyAction;
        Output = info.Output;
        Functions = options;
        UpdateLive(info);
        SelectedFunction = options.FirstOrDefault(option => option.Value == info.FunctionValue);
        suppressApply = false;
        Reset();
    }

    partial void OnMaxChanging(int value)
    {
        if (value == Max)
        {
            return;
        }

        IsDirty = true;
    }

    partial void OnMinChanging(int value)
    {
        if (value == Min)
        {
            return;
        }

        IsDirty = true;
    }


    partial void OnTrimChanging(int value)
    {
        if (value == Trim)
        {
            return;
        }

        IsDirty = true;
    }

    partial void OnOutputChanging(int value)
    {
        if (value == Output)
        {
            return;
        }

        IsDirty = true;
    }


    partial void OnIsDirtyChanged(bool value)
    {
        if (suppressApply)
        {
            return;
        }

        isDirtyAction(this);
    }

    ///
    /// <summary>Gets the one-based output number.
    /// </summary>
    [ObservableProperty]
    public partial int Output { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [ObservableProperty]
    public partial int Min { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [ObservableProperty]
    public partial int Trim { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [ObservableProperty]
    public partial int Max { get; set; }


    /// <summary>
    /// Gets the available function options.
    /// </summary>
    public IReadOnlyList<ServoFunctionOption> Functions { get; }

    /// <summary>
    /// Gets the live PWM description.
    /// </summary>
    [ObservableProperty]
    public partial string LiveDescription { get; private set; } = string.Empty;

    /// <summary>
    /// Gets or sets the selected function.
    /// </summary>
    [ObservableProperty]
    public partial ServoFunctionOption? SelectedFunction { get; set; }

    /// <summary>Gets the output header.</summary>
    public string Header => $"# {Output}";

    /// <summary>Updates the live PWM from a new projection.</summary>
    /// <param name="info">The output projection.</param>
    public void UpdateLive(ServoOutputInfo info)
    {
        LiveDescription = info.LivePwm is { } pwm ? $"{pwm} us{(info.IsStale ? " (stale)" : string.Empty)}" : "—";
    }

    partial void OnSelectedFunctionChanged(ServoFunctionOption? value)
    {
        if (!suppressApply && value is not null)
        {
            IsDirty = true;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public void Reset()
    {
        IsDirty = false;
    }
}
