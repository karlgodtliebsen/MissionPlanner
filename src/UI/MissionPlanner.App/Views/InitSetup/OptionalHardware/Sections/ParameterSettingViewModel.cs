using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Setup.MandatoryHardware;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>One explicit-apply metadata-backed setting.</summary>
public sealed partial class ParameterSettingViewModel(PeripheralSetting setting, Func<PeripheralSetting, double, Task> apply) : ObservableObject
{
    public string Name => setting.Name;
    public string DisplayName => setting.DisplayName;
    public double CurrentValue => setting.CurrentValue;
    [ObservableProperty] public partial double PendingValue { get; set; } = setting.CurrentValue;

    [RelayCommand]
    private Task ApplyAsync()
    {
        return apply(setting, PendingValue);
    }
}

