using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;
using MissionPlanner.Core.Setup.MandatoryHardware;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>One explicit-apply setting retaining firmware types, options, bitmask and numeric metadata.</summary>
public sealed partial class ParameterSettingViewModel : ObservableObject
{
    private readonly PeripheralSetting setting;
    private readonly Func<PeripheralSetting, double, CancellationToken, Task> apply;

    /// <summary>Creates a setting editor with a cancellable owner-provided apply operation.</summary>
    /// <param name="setting">The reported setting and metadata.</param>
    /// <param name="apply">The readback-confirmed write operation.</param>
    public ParameterSettingViewModel(PeripheralSetting setting, Func<PeripheralSetting, double, CancellationToken, Task> apply)
    {
        this.setting = setting;
        this.apply = apply;
        Editor = new PeripheralSettingViewModel(setting, _ => Task.CompletedTask);
    }

    /// <summary>Gets the shared metadata-aware editor.</summary>
    public PeripheralSettingViewModel Editor { get; }

    /// <summary>Gets the actual parameter name, including sensor instance.</summary>
    public string Name => setting.Name;

    /// <summary>Gets the display name.</summary>
    public string DisplayName => setting.DisplayName;

    /// <summary>Gets the confirmed value.</summary>
    public double CurrentValue => setting.CurrentValue;

    private bool CanApply() => Editor.CanEdit;

    [RelayCommand(CanExecute = nameof(CanApply), IncludeCancelCommand = true)]
    private Task ApplyAsync(CancellationToken cancellationToken)
    {
        return apply(setting, Editor.Value, cancellationToken);
    }
}
