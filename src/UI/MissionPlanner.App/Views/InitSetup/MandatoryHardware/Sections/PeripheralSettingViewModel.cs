using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.Core.Setup.MandatoryHardware;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Presents one editable peripheral setting with either options or numeric entry.</summary>
public sealed partial class PeripheralSettingViewModel : ObservableObject
{
    private readonly Func<(string, double), Task> action;
    private readonly PeripheralSetting setting;
    private double confirmedValue;
    private bool updatingBits;

    /// <summary>Initializes a peripheral setting row.</summary>
    /// <param name="setting">The setting projection.</param>
    /// <param name="action">The owning workflow.</param>
    public PeripheralSettingViewModel(PeripheralSetting setting, Func<(string, double), Task> action)
    {
        this.setting = setting;
        this.action = action;
        confirmedValue = setting.CurrentValue;
        Bits = setting.Bits.Where(bit => bit.Key is >= 0 and < 32)
            .Select(bit => new PeripheralBitViewModel(bit.Key, bit.Value, ToggleBit)).ToArray();
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
    public bool HasOptions => setting.Options.Count > 0 && !HasBits;

    /// <summary>Gets whether the setting is free numeric entry.</summary>
    public bool IsNumeric => !HasOptions;

    /// <summary>Gets whether the rich editor should show numeric entry instead of named bits.</summary>
    public bool IsNumericEditor => IsNumeric && !HasBits;

    /// <summary>Gets whether a named bitmask editor is available.</summary>
    public bool HasBits => Bits.Count > 0;
    /// <summary>Gets the individually editable bits.</summary>
    public IReadOnlyList<PeripheralBitViewModel> Bits { get; }
    /// <summary>Gets detailed help.</summary>
    public string Description => setting.Description;
    /// <summary>Gets units and range guidance.</summary>
    public string ValueGuidance => $"{setting.Units}  Range: {setting.Minimum?.ToString() ?? "—"} … {setting.Maximum?.ToString() ?? "—"}";
    /// <summary>Gets the recommended increment.</summary>
    public decimal Increment => ToDecimal(setting.Increment);
    /// <summary>Gets the editor lower bound, retaining an existing out-of-range value for review.</summary>
    public decimal Minimum => setting.Minimum is { } min ? ToDecimal(Math.Min(min, setting.CurrentValue)) : decimal.MinValue;
    /// <summary>Gets the editor upper bound, retaining an existing out-of-range value for review.</summary>
    public decimal Maximum => setting.Maximum is { } max ? ToDecimal(Math.Max(max, setting.CurrentValue)) : decimal.MaxValue;
    /// <summary>Gets whether the entered value falls outside the recommended firmware range.</summary>
    public bool IsOutsideRange => setting.Minimum is { } min && Value < min || setting.Maximum is { } max && Value > max;
    /// <summary>Gets the proposed numeric value.</summary>
    public double Value => HasOptions ? SelectedOption?.Value ?? confirmedValue : NumericValue;
    /// <summary>Gets whether the row has pending edits.</summary>
    public bool IsDirty => Value != confirmedValue;
    /// <summary>Gets or sets whether the row sorts before other settings.</summary>
    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    /// <summary>Records a successfully applied value without discarding a newer edit.</summary>
    public void AcceptValue(double value)
    {
        confirmedValue = value;
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsOutsideRange));
    }

    partial void OnNumericValueChanged(double value)
    {
        updatingBits = true;
        var mask = double.IsFinite(value) && value >= 0 && value <= uint.MaxValue ? (uint)value : 0;
        foreach (var bit in Bits)
        {
            bit.IsSelected = (mask & (1u << bit.Position)) != 0;
        }
        updatingBits = false;
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsOutsideRange));
    }

    partial void OnSelectedOptionChanged(PeripheralSettingOption? value) => OnPropertyChanged(nameof(IsDirty));

    private void ToggleBit(int position, bool enabled)
    {
        if (updatingBits)
        {
            return;
        }
        var mask = double.IsFinite(NumericValue) && NumericValue >= 0 && NumericValue <= uint.MaxValue ? (uint)NumericValue : 0;
        NumericValue = enabled ? mask | (1u << position) : mask & ~(1u << position);
    }

    private static decimal ToDecimal(double value) => !double.IsFinite(value) ? 0
        : value >= (double)decimal.MaxValue ? decimal.MaxValue
        : value <= (double)decimal.MinValue ? decimal.MinValue : (decimal)value;

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
        var value = Value;
        return action.Invoke((setting.Name, value));
    }
}

/// <summary>Presents one named bit while preserving all other bits in the parameter.</summary>
public sealed partial class PeripheralBitViewModel(int position, string name, Action<int, bool> changed) : ObservableObject
{
    /// <summary>Gets the bit position.</summary>
    public int Position { get; } = position;
    /// <summary>Gets the firmware label.</summary>
    public string Name { get; } = name;
    /// <summary>Gets or sets the bit state.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
    partial void OnIsSelectedChanged(bool value) => changed(Position, value);
}

