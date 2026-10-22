using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Setup.MandatoryHardware;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>One explicit-apply metadata-backed setting.</summary>
public sealed partial class ParameterSettingViewModel(PeripheralSetting setting, Func<PeripheralSetting, double, Task> apply) : ObservableObject
{
    /// <summary>Gets the parameter name.</summary>
    public string Name => setting.Name;
    /// <summary>Gets the display name.</summary>
    public string DisplayName => setting.DisplayName;
    /// <summary>Gets the current value.</summary>
    public double CurrentValue => setting.CurrentValue;

    [ObservableProperty]
    public partial double PendingValue { get; set; } = setting.CurrentValue;

    [RelayCommand]
    private Task ApplyAsync()
    {
        return apply(setting, PendingValue);
    }
}

