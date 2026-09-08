#nullable enable

namespace UraniumUI.Material.Controls;

public partial class NumericLabel
{
    /// <summary>Gets or sets the textual numeric representation, such as Double, Int16, or UInt8.</summary>
    public string NumericType { get => (string)GetValue(NumericTypeProperty); set => SetValue(NumericTypeProperty, value); }

    /// <summary>Identifies the <see cref="NumericType"/> bindable property.</summary>
    public static readonly BindableProperty NumericTypeProperty = BindableProperty.Create(
        nameof(NumericType), typeof(string), typeof(NumericLabel), "Double",
        propertyChanged: static (bindable, _, _) => ((NumericLabel)bindable).OnNumericTypeChanged());

    /// <summary>Gets or sets the numeric value.</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Identifies the <see cref="Value"/> bindable property.</summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(double), typeof(NumericLabel), 0d, BindingMode.TwoWay,
        coerceValue: static (bindable, value) => ((NumericLabel)bindable).CoerceValue((double)value),
        propertyChanged: static (bindable, oldValue, newValue) =>
            ((NumericLabel)bindable).OnValueChanged((double)oldValue, (double)newValue));

    /// <summary>Gets or sets the minimum displayed value.</summary>
    public double Min { get => (double)GetValue(MinProperty); set => SetValue(MinProperty, value); }

    /// <summary>Identifies the <see cref="Min"/> bindable property.</summary>
    public static readonly BindableProperty MinProperty = BindableProperty.Create(
        nameof(Min), typeof(double), typeof(NumericLabel), double.MinValue,
        propertyChanged: static (bindable, _, _) => ((NumericLabel)bindable).OnRangeChanged());

    /// <summary>Gets or sets the maximum displayed value.</summary>
    public double Max { get => (double)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>Identifies the <see cref="Max"/> bindable property.</summary>
    public static readonly BindableProperty MaxProperty = BindableProperty.Create(
        nameof(Max), typeof(double), typeof(NumericLabel), double.MaxValue,
        propertyChanged: static (bindable, _, _) => ((NumericLabel)bindable).OnRangeChanged());

    /// <summary>
    /// Gets or sets the format used for floating-point values, such as <c>G7</c>, <c>F7</c>,
    /// or <c>N2</c>. Integer numeric types always use their natural integer representation.
    /// </summary>
    public string NumberFormat { get => (string)GetValue(NumberFormatProperty); set => SetValue(NumberFormatProperty, value); }

    /// <summary>Identifies the <see cref="NumberFormat"/> bindable property.</summary>
    public static readonly BindableProperty NumberFormatProperty = BindableProperty.Create(
        nameof(NumberFormat), typeof(string), typeof(NumericLabel), "G15",
        propertyChanged: static (bindable, _, _) => ((NumericLabel)bindable).FormatValue());

    /// <summary>Gets or sets an optional culture name. Empty uses the current culture.</summary>
    public string CultureName { get => (string)GetValue(CultureNameProperty); set => SetValue(CultureNameProperty, value); }

    /// <summary>Identifies the <see cref="CultureName"/> bindable property.</summary>
    public static readonly BindableProperty CultureNameProperty = BindableProperty.Create(
        nameof(CultureName), typeof(string), typeof(NumericLabel), string.Empty,
        propertyChanged: static (bindable, _, _) => ((NumericLabel)bindable).FormatValue());
}
