using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Setup.MandatoryHardware;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>A semantic compass choice with local pending state and metadata-only defaults.</summary>
public sealed partial class CompassSettingViewModel : ObservableObject
{
    private readonly Action changed;

    /// <summary>Creates a local editor without writing to the vehicle.</summary>
    public CompassSettingViewModel(CompassSettingDefinition definition, double? pending, Action changed)
    {
        Definition = definition;
        this.changed = changed;
        selected = definition.Choices.FirstOrDefault(choice => choice.Value == pending);
    }

    /// <summary>Semantic capability and secondary raw diagnostics.</summary>
    public CompassSettingDefinition Definition { get; }
    /// <summary>Friendly control label.</summary>
    public string Label => Definition.Label;
    /// <summary>Control explanation.</summary>
    public string Description => Definition.Description;
    /// <summary>Firmware-supported choices.</summary>
    public IReadOnlyList<CompassChoice> Choices => Definition.Choices;
    /// <summary>Whether a Boolean editor is appropriate.</summary>
    public bool IsToggle => Definition.Setting is CompassSetting.Enabled or CompassSetting.PrimaryUse or CompassSetting.SecondaryUse or CompassSetting.TertiaryUse;
    /// <summary>Whether this instance is relevant to the connected firmware.</summary>
    public bool IsVisible => Definition.Current is not null || Definition.Setting is CompassSetting.Enabled or CompassSetting.PrimaryUse or CompassSetting.PrimaryOrientation or CompassSetting.YawSource;
    /// <summary>Boolean pending value; null represents an unknown current value.</summary>
    public bool? IsChecked
    {
        get => Selected is null ? null : Selected.Value != 0;
        set
        {
            if (value is { } enabled)
            {
                Selected = Choices.FirstOrDefault(choice => choice.Value == (enabled ? 1 : 0));
            }
        }
    }
    /// <summary>Whether the firmware supports editing this setting.</summary>
    public bool CanEdit => Definition.CanEdit;
    /// <summary>Capability explanation.</summary>
    public string? UnavailableReason => Definition.UnavailableReason;
    /// <summary>Secondary raw parameter name/current value.</summary>
    public string ParameterText => $"{Definition.ParameterName} = {Definition.Current?.ToString() ?? "Unavailable"}";
    /// <summary>Friendly default text, never guessed.</summary>
    public string DefaultText => $"Default: {Friendly(Definition.Default)}";
    /// <summary>Current-to-pending marker.</summary>
    public string ChangeText => IsChanged ? $"{Friendly(Definition.Current)} → {Friendly(Selected?.Value)} · Changed" : $"Current: {Friendly(Definition.Current)}";
    /// <summary>Whether the setting differs from the confirmed current value.</summary>
    public bool IsChanged => Selected is not null && Selected.Value != Definition.Current;
    /// <summary>Whether a valid metadata default is available.</summary>
    public bool CanReset => CanEdit && Definition.Default is { } value && Choices.Any(choice => choice.Value == value);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChecked))]
    [NotifyPropertyChangedFor(nameof(IsChanged))]
    [NotifyPropertyChangedFor(nameof(ChangeText))]
    private CompassChoice? selected;

    partial void OnSelectedChanged(CompassChoice? value) => changed();

    /// <summary>Stages the firmware-provided default without applying it.</summary>
    [RelayCommand(CanExecute = nameof(CanReset))]
    private void ResetDefault()
    {
        Selected = Choices.First(choice => choice.Value == Definition.Default);
    }

    private string Friendly(double? value) => value is null ? "Unknown" : Choices.FirstOrDefault(choice => choice.Value == value)?.Label ?? $"Unrecognized ({value})";
}
