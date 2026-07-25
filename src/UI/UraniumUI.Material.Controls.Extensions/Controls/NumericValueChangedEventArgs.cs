#nullable enable

namespace UraniumUI.Material.Controls;

public sealed class NumericValueChangedEventArgs(
    double oldValue,
    double newValue) : EventArgs
{
    public double OldValue { get; } = oldValue;

    public double NewValue { get; } = newValue;
}
