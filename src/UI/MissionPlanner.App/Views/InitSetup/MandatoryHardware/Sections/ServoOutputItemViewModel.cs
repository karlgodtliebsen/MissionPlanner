using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.Setup;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Presents one servo output with a function picker and live PWM.</summary>
public sealed partial class ServoOutputItemViewModel : ObservableObject
{
    private readonly bool suppressApply;

    /// <summary>
    /// Gets a value indicating whether the servo output has been modified.
    /// </summary>
    public bool IsDirty { get; private set; }

    /// <summary>Initializes a servo output row.</summary>
    /// <param name="info">The output projection.</param>
    /// <param name="options">The available functions.</param>
    public ServoOutputItemViewModel(ServoOutputInfo info, IReadOnlyList<ServoFunctionOption> options)
    {
        Output = info.Output;
        Functions = options;
        UpdateLive(info);
        suppressApply = true;
        SelectedFunction = options.FirstOrDefault(option => option.Value == info.FunctionValue);
        suppressApply = false;
    }

    /// <summary>Gets the one-based output number.</summary>
    [ObservableProperty]
    public partial int Output { get; set; }


    [ObservableProperty] public partial int Min { get; set; }

    [ObservableProperty] public partial int Trim { get; set; }

    [ObservableProperty] public partial int Max { get; set; }


    /// <summary>Gets the available function options.</summary>
    public IReadOnlyList<ServoFunctionOption> Functions { get; }

    /// <summary>Gets the output header.</summary>
    public string Header => $"# {Output}";

    /// <summary>Gets the live PWM description.</summary>
    [ObservableProperty]
    public partial string LiveDescription { get; private set; } = string.Empty;

    /// <summary>Gets or sets the selected function.</summary>
    [ObservableProperty]
    public partial ServoFunctionOption? SelectedFunction { get; set; }

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
