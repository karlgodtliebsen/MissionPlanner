#nullable enable

using System.Windows.Input;

namespace UraniumUI.Material.Controls;

public partial class NumericUpDownField
{
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value),
        typeof(double),
        typeof(NumericUpDownField),
        0d,
        BindingMode.TwoWay,
        coerceValue: static (bindable, value) =>
            ((NumericUpDownField)bindable).ClampAndNormalize((double)value),
        propertyChanged: static (bindable, oldValue, newValue) =>
            ((NumericUpDownField)bindable).OnValueChanged(
                (double)oldValue,
                (double)newValue));

    /// <summary>
    /// Minimum permitted value.
    /// </summary>
    public double Min
    {
        get => (double)GetValue(MinProperty);
        set => SetValue(MinProperty, value);
    }

    public static readonly BindableProperty MinProperty = BindableProperty.Create(
        nameof(Min),
        typeof(double),
        typeof(NumericUpDownField),
        double.MinValue,
        propertyChanged: static (bindable, _, _) =>
            ((NumericUpDownField)bindable).OnRangeChanged());

    /// <summary>
    /// Maximum permitted value.
    /// </summary>
    public double Max
    {
        get => (double)GetValue(MaxProperty);
        set => SetValue(MaxProperty, value);
    }

    public static readonly BindableProperty MaxProperty = BindableProperty.Create(
        nameof(Max),
        typeof(double),
        typeof(NumericUpDownField),
        double.MaxValue,
        propertyChanged: static (bindable, _, _) =>
            ((NumericUpDownField)bindable).OnRangeChanged());

    /// <summary>
    /// Gets or sets the positive amount applied by each increment or decrement operation.
    /// </summary>
    public double StepSize
    {
        get => (double)GetValue(StepSizeProperty);
        set => SetValue(StepSizeProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="StepSize"/> bindable property.
    /// </summary>
    public static readonly BindableProperty StepSizeProperty = BindableProperty.Create(
        nameof(StepSize),
        typeof(double),
        typeof(NumericUpDownField),
        1d,
        coerceValue: static (_, value) =>
        {
            var step = Math.Abs((double)value);
            return double.IsFinite(step) && step > 0 ? step : 1d;
        },
        propertyChanged: static (bindable, _, _) =>
            ((NumericUpDownField)bindable).OnStepPrecisionChanged());


    /// <summary>
    /// When true and <see cref="DecimalPlaces"/> is -1, the control derives its
    /// comparison, display and stepping precision from <see cref="StepSize"/>.
    ///
    /// For example, StepSize 0.1 uses one decimal place, while StepSize 0.001
    /// uses three decimal places. This hides float32 round-trip noise commonly
    /// seen in MAVLink parameter values.
    /// </summary>
    public bool UseStepSizePrecision
    {
        get => (bool)GetValue(UseStepSizePrecisionProperty);
        set => SetValue(UseStepSizePrecisionProperty, value);
    }

    public static readonly BindableProperty UseStepSizePrecisionProperty =
        BindableProperty.Create(
            nameof(UseStepSizePrecision),
            typeof(bool),
            typeof(NumericUpDownField),
            true,
            propertyChanged: static (bindable, _, _) =>
                ((NumericUpDownField)bindable).OnStepPrecisionChanged());

    /// <summary>
    /// Number of decimal places used for rounding and formatting.
    /// Set to -1 to use <see cref="NumberFormat"/>.
    /// </summary>
    public int DecimalPlaces
    {
        get => (int)GetValue(DecimalPlacesProperty);
        set => SetValue(DecimalPlacesProperty, value);
    }

    public static readonly BindableProperty DecimalPlacesProperty =
        BindableProperty.Create(
            nameof(DecimalPlaces),
            typeof(int),
            typeof(NumericUpDownField),
            -1,
            coerceValue: static (_, value) => Math.Clamp((int)value, -1, 15),
            propertyChanged: static (bindable, _, _) =>
                ((NumericUpDownField)bindable).ReformatValue());

    /// <summary>
    /// Numeric format used when <see cref="DecimalPlaces"/> is -1.
    /// </summary>
    public string NumberFormat
    {
        get => (string)GetValue(NumberFormatProperty);
        set => SetValue(NumberFormatProperty, value);
    }

    public static readonly BindableProperty NumberFormatProperty =
        BindableProperty.Create(
            nameof(NumberFormat),
            typeof(string),
            typeof(NumericUpDownField),
            "G15",
            propertyChanged: static (bindable, _, _) =>
                ((NumericUpDownField)bindable).ReformatValue());

    /// <summary>
    /// Optional .NET culture name, such as "da-DK" or "en-US".
    /// An empty value uses CultureInfo.CurrentCulture.
    /// </summary>
    public string CultureName
    {
        get => (string)GetValue(CultureNameProperty);
        set => SetValue(CultureNameProperty, value);
    }

    public static readonly BindableProperty CultureNameProperty =
        BindableProperty.Create(
            nameof(CultureName),
            typeof(string),
            typeof(NumericUpDownField),
            string.Empty,
            propertyChanged: static (bindable, _, _) =>
                ((NumericUpDownField)bindable).ReformatValue());

    /// <summary>
    /// Determines whether stepping beyond one end continues from the other end.
    /// </summary>
    public bool IsWrapEnabled
    {
        get => (bool)GetValue(IsWrapEnabledProperty);
        set => SetValue(IsWrapEnabledProperty, value);
    }

    public static readonly BindableProperty IsWrapEnabledProperty =
        BindableProperty.Create(
            nameof(IsWrapEnabled),
            typeof(bool),
            typeof(NumericUpDownField),
            false,
            propertyChanged: static (bindable, _, _) =>
                ((NumericUpDownField)bindable).UpdateCommandStates());

    /// <summary>
    /// Normalizes and clamps manually entered text when editing completes or the
    /// field loses focus.
    /// </summary>
    public bool ClampOnCommit
    {
        get => (bool)GetValue(ClampOnCommitProperty);
        set => SetValue(ClampOnCommitProperty, value);
    }

    public static readonly BindableProperty ClampOnCommitProperty =
        BindableProperty.Create(
            nameof(ClampOnCommit),
            typeof(bool),
            typeof(NumericUpDownField),
            true);

    public TextAlignment VerticalTextAlignment
    {
        get => (TextAlignment)GetValue(VerticalTextAlignmentProperty);
        set => SetValue(VerticalTextAlignmentProperty, value);
    }

    public static readonly BindableProperty VerticalTextAlignmentProperty =
        BindableProperty.Create(
            nameof(VerticalTextAlignment),
            typeof(TextAlignment),
            typeof(NumericUpDownField),
            Entry.VerticalTextAlignmentProperty.DefaultValue);

    public bool ShowStepperButtons
    {
        get => (bool)GetValue(ShowStepperButtonsProperty);
        set => SetValue(ShowStepperButtonsProperty, value);
    }

    public static readonly BindableProperty ShowStepperButtonsProperty =
        BindableProperty.Create(
            nameof(ShowStepperButtons),
            typeof(bool),
            typeof(NumericUpDownField),
            true,
            propertyChanged: static (bindable, _, _) =>
                ((NumericUpDownField)bindable).UpdateStepperVisibility());

    public NumericUpDownButtonOrientation ButtonOrientation
    {
        get => (NumericUpDownButtonOrientation)GetValue(ButtonOrientationProperty);
        set => SetValue(ButtonOrientationProperty, value);
    }

    public static readonly BindableProperty ButtonOrientationProperty =
        BindableProperty.Create(
            nameof(ButtonOrientation),
            typeof(NumericUpDownButtonOrientation),
            typeof(NumericUpDownField),
            NumericUpDownButtonOrientation.Horizontal,
            propertyChanged: static (bindable, _, _) =>
                ((NumericUpDownField)bindable).RebuildStepper());

    public string IncrementText
    {
        get => (string)GetValue(IncrementTextProperty);
        set => SetValue(IncrementTextProperty, value);
    }

    public static readonly BindableProperty IncrementTextProperty =
        BindableProperty.Create(
            nameof(IncrementText),
            typeof(string),
            typeof(NumericUpDownField),
            "\u002B", //"＋",
            propertyChanged: static (bindable, _, _) =>
                ((NumericUpDownField)bindable).UpdateStepperAppearance());

    public string DecrementText
    {
        get => (string)GetValue(DecrementTextProperty);
        set => SetValue(DecrementTextProperty, value);
    }

    public static readonly BindableProperty DecrementTextProperty =
        BindableProperty.Create(
            nameof(DecrementText),
            typeof(string),
            typeof(NumericUpDownField),
            "\u2212", //"−",
            propertyChanged: static (bindable, _, _) =>
                ((NumericUpDownField)bindable).UpdateStepperAppearance());

    public double StepButtonWidth
    {
        get => (double)GetValue(StepButtonWidthProperty);
        set => SetValue(StepButtonWidthProperty, value);
    }

    public static readonly BindableProperty StepButtonWidthProperty =
        BindableProperty.Create(
            nameof(StepButtonWidth),
            typeof(double),
            typeof(NumericUpDownField),
            20d,
            coerceValue: static (_, value) => Math.Max(20d, (double)value),
            propertyChanged: static (bindable, _, _) =>
                ((NumericUpDownField)bindable).RebuildStepper());

    public double StepButtonHeight
    {
        get => (double)GetValue(StepButtonHeightProperty);
        set => SetValue(StepButtonHeightProperty, value);
    }

    public static readonly BindableProperty StepButtonHeightProperty =
        BindableProperty.Create(
            nameof(StepButtonHeight),
            typeof(double),
            typeof(NumericUpDownField),
            18d,
            coerceValue: static (_, value) => Math.Max(18d, (double)value),
            propertyChanged: static (bindable, _, _) =>
                ((NumericUpDownField)bindable).RebuildStepper());

    public double StepButtonFontSize
    {
        get => (double)GetValue(StepButtonFontSizeProperty);
        set => SetValue(StepButtonFontSizeProperty, value);
    }

    public static readonly BindableProperty StepButtonFontSizeProperty =
        BindableProperty.Create(
            nameof(StepButtonFontSize),
            typeof(double),
            typeof(NumericUpDownField),
            20d,
            propertyChanged: static (bindable, _, _) =>
                ((NumericUpDownField)bindable).UpdateStepperAppearance());

    public Color StepButtonBackgroundColor
    {
        get => (Color)GetValue(StepButtonBackgroundColorProperty);
        set => SetValue(StepButtonBackgroundColorProperty, value);
    }

    public static readonly BindableProperty StepButtonBackgroundColorProperty =
        BindableProperty.Create(
            nameof(StepButtonBackgroundColor),
            typeof(Color),
            typeof(NumericUpDownField),
            Colors.Transparent,
            propertyChanged: static (bindable, _, _) =>
                ((NumericUpDownField)bindable).UpdateStepperAppearance());

    private static readonly BindablePropertyKey IsTextValidPropertyKey =
        BindableProperty.CreateReadOnly(
            nameof(IsTextValid),
            typeof(bool),
            typeof(NumericUpDownField),
            true);

    public static readonly BindableProperty IsTextValidProperty =
        IsTextValidPropertyKey.BindableProperty;

    public bool IsTextValid => (bool)GetValue(IsTextValidProperty);

    public ICommand IncrementCommand { get; private set; } = null!;

    public ICommand DecrementCommand { get; private set; } = null!;
}
