#nullable enable

namespace UraniumUI.Material.Controls;

public partial class NumericField
{
    /// <summary>Gets or sets the numeric value.</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Identifies the <see cref="Value"/> bindable property.</summary>
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(double), typeof(NumericField), 0d, BindingMode.TwoWay,
        coerceValue: static (bindable, value) => ((NumericField)bindable).CoerceValue((double)value),
        propertyChanged: static (bindable, oldValue, newValue) =>
            ((NumericField)bindable).OnValueChanged((double)oldValue, (double)newValue));

    /// <summary>Gets or sets the minimum accepted value.</summary>
    public double Min { get => (double)GetValue(MinProperty); set => SetValue(MinProperty, value); }

    /// <summary>Identifies the <see cref="Min"/> bindable property.</summary>
    public static readonly BindableProperty MinProperty = BindableProperty.Create(
        nameof(Min), typeof(double), typeof(NumericField), double.MinValue,
        propertyChanged: static (bindable, _, _) => ((NumericField)bindable).OnRangeChanged());

    /// <summary>Gets or sets the maximum accepted value.</summary>
    public double Max { get => (double)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    /// <summary>Identifies the <see cref="Max"/> bindable property.</summary>
    public static readonly BindableProperty MaxProperty = BindableProperty.Create(
        nameof(Max), typeof(double), typeof(NumericField), double.MaxValue,
        propertyChanged: static (bindable, _, _) => ((NumericField)bindable).OnRangeChanged());

    /// <summary>Gets or sets the format applied after editing, such as <c>F7</c> or <c>N2</c>.</summary>
    public string NumberFormat { get => (string)GetValue(NumberFormatProperty); set => SetValue(NumberFormatProperty, value); }

    /// <summary>Identifies the <see cref="NumberFormat"/> bindable property.</summary>
    public static readonly BindableProperty NumberFormatProperty = BindableProperty.Create(
        nameof(NumberFormat), typeof(string), typeof(NumericField), "G15",
        propertyChanged: static (bindable, _, _) => ((NumericField)bindable).FormatValueWhenNotEditing());

    /// <summary>Gets or sets an optional culture name. Empty uses the current UI culture.</summary>
    public string CultureName { get => (string)GetValue(CultureNameProperty); set => SetValue(CultureNameProperty, value); }

    /// <summary>Identifies the <see cref="CultureName"/> bindable property.</summary>
    public static readonly BindableProperty CultureNameProperty = BindableProperty.Create(
        nameof(CultureName), typeof(string), typeof(NumericField), string.Empty,
        propertyChanged: static (bindable, _, _) => ((NumericField)bindable).FormatValueWhenNotEditing());

    /// <summary>Gets or sets whether a leading positive or negative sign is accepted.</summary>
    public bool AllowSign { get => (bool)GetValue(AllowSignProperty); set => SetValue(AllowSignProperty, value); }

    /// <summary>Identifies the <see cref="AllowSign"/> bindable property.</summary>
    public static readonly BindableProperty AllowSignProperty = BindableProperty.Create(
        nameof(AllowSign), typeof(bool), typeof(NumericField), true);

    /// <summary>Gets or sets whether the culture-specific thousands separator is accepted.</summary>
    public bool AllowThousands { get => (bool)GetValue(AllowThousandsProperty); set => SetValue(AllowThousandsProperty, value); }

    /// <summary>Identifies the <see cref="AllowThousands"/> bindable property.</summary>
    public static readonly BindableProperty AllowThousandsProperty = BindableProperty.Create(
        nameof(AllowThousands), typeof(bool), typeof(NumericField), true);

    /// <summary>Gets or sets whether commit clamps values to <see cref="Min"/> and <see cref="Max"/>.</summary>
    public bool ClampOnCommit { get => (bool)GetValue(ClampOnCommitProperty); set => SetValue(ClampOnCommitProperty, value); }

    /// <summary>Identifies the <see cref="ClampOnCommit"/> bindable property.</summary>
    public static readonly BindableProperty ClampOnCommitProperty = BindableProperty.Create(
        nameof(ClampOnCommit), typeof(bool), typeof(NumericField), true);

    private static readonly BindablePropertyKey IsTextValidPropertyKey = BindableProperty.CreateReadOnly(
        nameof(IsTextValid), typeof(bool), typeof(NumericField), true);

    /// <summary>Identifies the read-only <see cref="IsTextValid"/> bindable property.</summary>
    public static readonly BindableProperty IsTextValidProperty = IsTextValidPropertyKey.BindableProperty;

    /// <summary>Gets whether the current editing text is a complete in-range number.</summary>
    public bool IsTextValid => (bool)GetValue(IsTextValidProperty);
}
