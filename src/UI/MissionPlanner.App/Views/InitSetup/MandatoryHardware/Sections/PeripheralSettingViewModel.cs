using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Setup.MandatoryHardware;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Presents one editable peripheral setting with either options or numeric entry.</summary>
public sealed partial class PeripheralSettingViewModel : ObservableObject
{
    private readonly Action<(string, double)> action;
    private readonly PeripheralSetting setting;

    /// <summary>Initializes a peripheral setting row.</summary>
    /// <param name="setting">The setting projection.</param>
    /// <param name="action">The owning workflow.</param>
    public PeripheralSettingViewModel(PeripheralSetting setting, Action<(string, double)> action)
    {
        this.setting = setting;
        this.action = action;
        NumericValue = setting.CurrentValue;
        SelectedOption = setting.Options.FirstOrDefault(option => Math.Abs(option.Value - setting.CurrentValue) <= 0.0005);
    }

    /// <summary>Gets the parameter display name.</summary>
    public string DisplayName => setting.DisplayName;

    /// <summary>Gets the parameter name.</summary>
    public string Name => setting.Name;

    /// <summary>Gets the metadata options.</summary>
    public IReadOnlyList<PeripheralSettingOption> Options => setting.Options;

    /// <summary>Gets whether the setting exposes discrete options.</summary>
    public bool HasOptions => setting.Options.Count > 0;

    /// <summary>Gets whether the setting is free numeric entry.</summary>
    public bool IsNumeric => setting.Options.Count == 0;

    /// <summary>Gets whether the setting is sensitive.</summary>
    public bool IsSecret => setting.IsSecret;

    /// <summary>Gets whether a reboot is required after changing this setting.</summary>
    public bool RebootRequired => setting.RebootRequired;

    /// <summary>Gets or sets the selected discrete option.</summary>
    [ObservableProperty]
    public partial PeripheralSettingOption? SelectedOption { get; set; }

    /// <summary>Gets or sets the free numeric value.</summary>
    [ObservableProperty]
    public partial double NumericValue { get; set; }

    [RelayCommand]
    private Task Apply()
    {
        var value = HasOptions ? SelectedOption?.Value ?? setting.CurrentValue : NumericValue;
        action.Invoke((setting.Name, value));
        return Task.CompletedTask;
    }
}
