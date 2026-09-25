using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Setup.Arming;

namespace MissionPlanner.App.Views.InitSetup.Arming;

/// <summary>One Arming semantic editor; edits stay local until reviewed Apply.</summary>
public sealed partial class ArmingSettingViewModel : ObservableObject
{
    private readonly Action<ArmingConfiguration> changed;
    private ArmingConfiguration desired;
    private ArmingChoice? selected;
    /// <summary>Builds a metadata-backed editor.</summary>
    public ArmingSettingViewModel(ArmingSettingDefinition definition, ArmingConfiguration desired, Action<ArmingConfiguration> changed)
    {
        Definition = definition;
        this.desired = desired;
        this.changed = changed;
        selected = Choices.FirstOrDefault(c => c.Value == desired.EditorValue(definition.Setting));
        Bits = definition.Bits.Select(b => new ArmingBitViewModel(b, (desired.CustomChecks & (int)b.Value) != 0, BitsChanged)).ToArray();
    }
    /// <summary>Firmware-derived setting definition.</summary>
    public ArmingSettingDefinition Definition { get; }
    /// <summary>Supported friendly values.</summary>
    public IReadOnlyList<ArmingChoice> Choices => Definition.Choices;
    /// <summary>Custom checks, excluding the special All bit.</summary>
    public IReadOnlyList<ArmingBitViewModel> Bits { get; }
    /// <summary>Whether this optional setting is exposed by firmware.</summary>
    public bool IsVisible => Definition.Current is not null || Definition.Setting is ArmingSetting.Checks or ArmingSetting.Stick;
    /// <summary>Whether to show the metadata bit editor.</summary>
    public bool IsCustom => Definition.Setting == ArmingSetting.Checks && desired.Checks == PreArmCheckMode.Custom;
    /// <summary>Confirmed value and metadata default; absent defaults remain unknown.</summary>
    public string Evidence => $"Current: {Definition.Choices.FirstOrDefault(c => c.Value == Definition.Current)?.Label ?? Definition.Current?.ToString() ?? "Unknown"} · Default: {Definition.Default?.ToString() ?? "Unknown"}" + (Definition.RequiresReboot ? " · Reboot required" : string.Empty);
    /// <summary>Whether this row differs semantically from the confirmed FC value.</summary>
    public bool IsChanged => desired.Get(Definition.Setting) != (Definition.Current is { } value
        ? new ArmingConfiguration().With(Definition.Setting, value).Get(Definition.Setting) : null);
    /// <summary>Explicit local change marker.</summary>
    public string PendingText => "Pending: " + (Selected?.Label ?? "Unknown") + " — not active until Apply";
    /// <summary>Whether a usable firmware default can be staged.</summary>
    public bool CanReset => Definition.CanEdit && Definition.Default is { } value &&
        (Choices.Any(c => c.Value == value) || Definition.Setting == ArmingSetting.Checks && value > 1 && (int)value == value &&
            ((int)value & ~Bits.Aggregate(0, (mask, b) => mask | (int)b.Choice.Value)) == 0);
    /// <summary>The locally selected friendly value.</summary>
    public ArmingChoice? Selected
    {
        get => selected;
        set
        {
            if (SetProperty(ref selected, value) && value is not null)
            {
                desired = desired.WithEditorValue(Definition.Setting, value.Value);
                OnPropertyChanged(nameof(IsCustom));
                changed(desired);
            }
        }
    }
    private void BitsChanged()
    {
        desired = desired with { Checks = PreArmCheckMode.Custom, CustomChecks = Bits.Where(b => b.IsChecked).Aggregate(0, (mask, b) => mask | (int)b.Choice.Value) };
        changed(desired);
    }
    [RelayCommand(CanExecute = nameof(CanReset))]
    private void ResetDefault()
    {
        desired = desired.With(Definition.Setting, Definition.Default!.Value);
        changed(desired);
    }
}

/// <summary>A metadata-derived custom check toggle.</summary>
public sealed class ArmingBitViewModel(ArmingChoice choice, bool enabled, Action changed) : ObservableObject
{
    private bool isChecked = enabled;
    /// <summary>Friendly metadata label and bit value.</summary>
    public ArmingChoice Choice { get; } = choice;
    /// <summary>Local pending selection.</summary>
    public bool IsChecked
    {
        get => isChecked;
        set
        {
            if (SetProperty(ref isChecked, value))
            {
                changed();
            }
        }
    }
}
