#nullable enable

namespace UraniumUI.Material.Controls;

/// <summary>Provides the previous and current values for a numeric value change.</summary>
/// <param name="oldValue">The value before the change.</param>
/// <param name="newValue">The value after the change.</param>
public sealed class NumericValueChangedEventArgs(
    double oldValue,
    double newValue) : EventArgs
{
    /// <summary>Gets the value before the change.</summary>
    public double OldValue { get; } = oldValue;

    /// <summary>Gets the value after the change.</summary>
    public double NewValue { get; } = newValue;
}
