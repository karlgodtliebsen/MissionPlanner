#nullable enable

using System.Globalization;
using System.Windows.Input;
using Plainer.Maui.Controls;

namespace UraniumUI.Material.Controls;

/// <summary>
/// A single-line Material numeric editor with decrement and increment actions
/// inside the same UraniumUI InputField border.
/// </summary>
/// <remarks>
/// Bind either <see cref="Value"/> or the inherited <see cref="TextField.Text"/>
/// property. Binding both is supported but usually unnecessary.
/// </remarks>
public partial class NumericUpDownField : TextField
{
    private readonly EntryView entry;
    private readonly ContentView stepperAttachment;

    private readonly Command incrementCommand;
    private readonly Command decrementCommand;

    private Button? incrementButton;
    private Button? decrementButton;

    private bool updatingTextFromValue;
    private bool updatingValueFromText;
    private bool ensuringStepperAttachment;
    private bool correctingRange;

    /// <summary>
    /// Initializes a new instance of the <see cref="NumericUpDownField"/> class.
    /// </summary>
    public NumericUpDownField()
    {
        entry = Content as EntryView
                ?? throw new InvalidOperationException(
                    $"{nameof(NumericUpDownField)} requires UraniumUI's EntryView content.");

        Keyboard = Keyboard.Numeric;
        AllowClear = false;
        ClearButtonVisibility = ClearButtonVisibility.Never;

        entry.SetBinding(
            Entry.VerticalTextAlignmentProperty,
            new Binding(nameof(VerticalTextAlignment), source: this));

        incrementCommand = new Command(
            Increment,
            CanIncrement);

        decrementCommand = new Command(
            Decrement,
            CanDecrement);

        IncrementCommand = incrementCommand;
        DecrementCommand = decrementCommand;

        stepperAttachment = new ContentView { Padding = 0, Margin = 0, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Fill };

        RebuildStepper();
        EnsureStepperAttachment();

        // Do not subscribe to this.PropertyChanged. A Material input raises many
        // presentation and layout property notifications. The numeric synchronization
        // is handled only by TextChanged and the Value bindable-property callback.
        TextChanged += NumericUpDownField_TextChanged;
        Completed += NumericUpDownField_Completed;
        entry.Unfocused += Entry_Unfocused;

        ReformatValue(false);
    }

    /// <summary>Occurs when <see cref="Value"/> changes materially.</summary>
    public event EventHandler<NumericValueChangedEventArgs>? ValueChanged;

    /// <inheritdoc />
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is not null)
        {
            // The rendered attachment container does not necessarily exist in the
            // constructor, so perform one idempotent check after template creation.
            EnsureStepperAttachment();
        }

        UpdateCommandStates();
    }

    private void NumericUpDownField_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (updatingTextFromValue ||
            string.Equals(
                e.OldTextValue,
                e.NewTextValue,
                StringComparison.Ordinal))
        {
            return;
        }

        if (!TryParseText(e.NewTextValue, out var parsed))
        {
            SetTextValidity(false);
            return;
        }

        var isWithinRange = parsed >= Min && parsed <= Max;
        SetTextValidity(isWithinRange);

        if (!isWithinRange)
        {
            return;
        }

        var normalized = Normalize(parsed);

        // Critical feedback-loop guard:
        // text "0,10" and numeric Value 0.1 already describe the same value.
        // Do not assign Value merely because the textual representation differs.
        if (NumericEquals(Value, normalized))
        {
            return;
        }

        updatingValueFromText = true;

        try
        {
            Value = normalized;
        }
        finally
        {
            updatingValueFromText = false;
        }
    }

    private void NumericUpDownField_Completed(object? sender, EventArgs e)
    {
        CommitText();
    }

    private void Entry_Unfocused(object? sender, FocusEventArgs e)
    {
        CommitText();
    }

    private void CommitText()
    {
        if (!TryParseText(Text, out var parsed))
        {
            // Invalid or incomplete text is replaced by the last accepted value.
            ReformatValue(true);
            return;
        }

        double candidate;

        if (ClampOnCommit)
        {
            candidate = ClampAndNormalize(parsed);
        }
        else if (parsed >= Min && parsed <= Max)
        {
            candidate = Normalize(parsed);
        }
        else
        {
            SetTextValidity(false);
            return;
        }

        SetNumericValueIfChanged(candidate);
        ReformatValue(true);
    }

    private void Increment()
    {
        Step(+1);
    }

    private void Decrement()
    {
        Step(-1);
    }

    private void Step(int direction)
    {
        if (IsReadOnly || !IsEnabled)
        {
            return;
        }

        var current = TryParseText(Text, out var parsed)
            ? parsed
            : Value;

        var candidate = CalculateSteppedValue(current, direction);

        if (IsWrapEnabled)
        {
            if (candidate > Max && !NumericEquals(candidate, Max))
            {
                candidate = Min;
            }
            else if (candidate < Min && !NumericEquals(candidate, Min))
            {
                candidate = Max;
            }
        }

        SetNumericValueIfChanged(ClampAndNormalize(candidate));
        ReformatValue(true);
        Focus();
    }

    /// <summary>
    /// Performs the actual step with decimal arithmetic. Before applying the step,
    /// the current value is rounded to the effective step precision. This turns a
    /// float32 round-trip such as 1.300000071526 back into 1.3 before adding 0.1.
    /// </summary>
    private double CalculateSteppedValue(double current, int direction)
    {
        var precision = GetEffectiveDecimalPlaces();

        if (TryConvertToDecimal(current, out var decimalCurrent) &&
            TryConvertToDecimal(StepSize, out var decimalStep))
        {
            decimalStep = decimal.Abs(decimalStep);

            if (precision >= 0)
            {
                decimalCurrent = decimal.Round(
                    decimalCurrent,
                    precision,
                    MidpointRounding.AwayFromZero);

                decimalStep = decimal.Round(
                    decimalStep,
                    precision,
                    MidpointRounding.AwayFromZero);
            }

            var stepped = decimalCurrent + (direction * decimalStep);

            if (precision >= 0)
            {
                stepped = decimal.Round(
                    stepped,
                    precision,
                    MidpointRounding.AwayFromZero);
            }

            return (double)stepped;
        }

        // Decimal cannot represent the entire double range. Keep a bounded
        // fallback for unusual scientific values.
        var fallback = current + (direction * StepSize);

        return precision >= 0
            ? Math.Round(
                fallback,
                precision,
                MidpointRounding.AwayFromZero)
            : Math.Round(
                fallback,
                12,
                MidpointRounding.AwayFromZero);
    }

    private bool CanIncrement()
    {
        return IsEnabled &&
               !IsReadOnly &&
               (IsWrapEnabled ||
                (Value < Max && !NumericEquals(Value, Max)));
    }

    private bool CanDecrement()
    {
        return IsEnabled &&
               !IsReadOnly &&
               (IsWrapEnabled ||
                (Value > Min && !NumericEquals(Value, Min)));
    }

    internal void UpdateCommandStates()
    {
        incrementCommand?.ChangeCanExecute();
        decrementCommand?.ChangeCanExecute();
    }

    internal void OnValueChanged(double oldValue, double newValue)
    {
        var materiallyChanged = !NumericEquals(oldValue, newValue);

        if (!updatingValueFromText)
        {
            // Preserve an equivalent user representation while editing. For example,
            // do not replace "0,10" with "0,1" merely because Value was assigned 0.1.
            ReformatValue(false);
        }

        SetTextValidity(true);
        UpdateCommandStates();

        if (materiallyChanged)
        {
            ValueChanged?.Invoke(
                this,
                new NumericValueChangedEventArgs(oldValue, newValue));
        }
    }

    internal void OnStepPrecisionChanged()
    {
        ReformatValue(false);
        UpdateCommandStates();
    }

    internal void OnRangeChanged()
    {
        if (correctingRange)
        {
            return;
        }

        correctingRange = true;

        try
        {
            if (Min > Max)
            {
                Max = Min;
            }

            var corrected = ClampAndNormalize(Value);

            if (!NumericEquals(Value, corrected))
            {
                Value = corrected;
            }
            else
            {
                ReformatValue(false);
            }

            UpdateCommandStates();
        }
        finally
        {
            correctingRange = false;
        }
    }

    internal double ClampAndNormalize(double value)
    {
        if (double.IsNaN(value))
        {
            return Value;
        }

        if (double.IsPositiveInfinity(value))
        {
            value = Max;
        }
        else if (double.IsNegativeInfinity(value))
        {
            value = Min;
        }

        return Normalize(Math.Clamp(value, Min, Max));
    }

    private double Normalize(double value)
    {
        if (DecimalPlaces >= 0)
        {
            return Math.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero);
        }

        // Prevent visible floating-point artifacts after repeated 0.1-style steps.
        return Math.Round(value, 12, MidpointRounding.AwayFromZero);
    }

    private bool SetNumericValueIfChanged(double candidate)
    {
        candidate = ClampAndNormalize(candidate);

        if (NumericEquals(Value, candidate))
        {
            return false;
        }

        Value = candidate;
        return true;
    }

    private bool NumericEquals(double left, double right)
    {
        if (left.Equals(right))
        {
            return true;
        }

        if (!double.IsFinite(left) || !double.IsFinite(right))
        {
            return false;
        }

        var precision = GetEffectiveDecimalPlaces();

        return precision >= 0
            ? RoundToPrecision(left, precision)
                .Equals(RoundToPrecision(right, precision))
            : Normalize(left).Equals(Normalize(right));
    }

    private int GetEffectiveDecimalPlaces()
    {
        if (DecimalPlaces >= 0)
        {
            return DecimalPlaces;
        }

        if (!UseStepSizePrecision ||
            !TryConvertToDecimal(StepSize, out var decimalStep))
        {
            return -1;
        }

        decimalStep = decimal.Abs(decimalStep);

        if (decimalStep == 0)
        {
            return -1;
        }

        var bits = decimal.GetBits(decimalStep);
        var scale = (bits[3] >> 16) & 0x7F;

        return Math.Clamp(scale, 0, 15);
    }

    private static double RoundToPrecision(double value, int precision)
    {
        return TryConvertToDecimal(value, out var decimalValue)
            ? (double)decimal.Round(
                decimalValue,
                precision,
                MidpointRounding.AwayFromZero)
            : Math.Round(
                value,
                precision,
                MidpointRounding.AwayFromZero);
    }

    private static bool TryConvertToDecimal(
        double value,
        out decimal result)
    {
        if (!double.IsFinite(value) ||
            value > (double)decimal.MaxValue ||
            value < (double)decimal.MinValue)
        {
            result = default;
            return false;
        }

        try
        {
            result = Convert.ToDecimal(value);
            return true;
        }
        catch (OverflowException)
        {
            result = default;
            return false;
        }
    }

    private bool TextRepresentsValue(string? text, double value)
    {
        return TryParseText(text, out var parsed) &&
               NumericEquals(parsed, value);
    }

    private void SetTextValidity(bool isValid)
    {
        if (IsTextValid != isValid)
        {
            SetValue(IsTextValidPropertyKey, isValid);
        }
    }

    internal void ReformatValue(bool forceCanonicalText = false)
    {
        // Semantic comparison comes before textual comparison. This prevents
        // culture/formatting ping-pong such as "0,10" <-> "0,1".
        if (!forceCanonicalText &&
            TextRepresentsValue(Text, Value))
        {
            SetTextValidity(true);
            return;
        }

        var formatted = FormatValue(Value);

        // The ordinary old-value check still matters because some ViewModels raise
        // PropertyChanged unconditionally from their setters.
        if (string.Equals(
                Text,
                formatted,
                StringComparison.Ordinal))
        {
            SetTextValidity(true);
            return;
        }

        updatingTextFromValue = true;

        try
        {
            Text = formatted;
            SetTextValidity(true);
        }
        finally
        {
            updatingTextFromValue = false;
        }
    }

    private string FormatValue(double value)
    {
        var culture = ResolveCulture();
        var precision = GetEffectiveDecimalPlaces();

        if (precision >= 0)
        {
            var rounded = RoundToPrecision(value, precision);

            // Fixed precision follows the declared step resolution:
            // 0.1 -> one decimal, 0.001 -> three decimals.
            return rounded.ToString(
                $"F{precision}",
                culture);
        }

        var format = string.IsNullOrWhiteSpace(NumberFormat)
            ? "G15"
            : NumberFormat;

        return value.ToString(format, culture);
    }

    private bool TryParseText(string? text, out double value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = default;
            return false;
        }

        const NumberStyles styles = NumberStyles.Number | NumberStyles.AllowThousands;

        var culture = ResolveCulture();

        if (double.TryParse(text, styles, culture, out value))
        {
            return true;
        }

        // ArduPilot parameter files commonly use invariant decimal points even when
        // the UI culture uses a decimal comma.
        return !Equals(culture, CultureInfo.InvariantCulture) && double.TryParse(text, styles, CultureInfo.InvariantCulture, out value);
    }

    private CultureInfo ResolveCulture()
    {
        if (string.IsNullOrWhiteSpace(CultureName))
        {
            return CultureInfo.CurrentCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(CultureName);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.CurrentCulture;
        }
    }

    private void RebuildStepper()
    {
        var view = ButtonOrientation switch
        {
            NumericUpDownButtonOrientation.Vertical => BuildVerticalStepper(),
            var _ => BuildHorizontalStepper()
        };

        stepperAttachment.Content = view;
        UpdateStepperAppearance();
        UpdateStepperVisibility();
        UpdateCommandStates();
    }

    private View BuildHorizontalStepper()
    {
        incrementButton = CreateStepButton(IncrementText, IncrementCommand, "Increase value");
        decrementButton = CreateStepButton(DecrementText, DecrementCommand, "Decrease value");

        //incrementButton.BorderColor = new Color(255, 0, 0, 0.5f);
        //decrementButton.BorderColor = new Color(0, 255, 0, 0.5f);

        //incrementButton.BorderWidth = 1;
        //decrementButton.BorderWidth = 1;

        //incrementButton.WidthRequest = StepButtonWidth;
        //incrementButton.HeightRequest = StepButtonHeight * 2;
        //decrementButton.WidthRequest = StepButtonWidth;
        //decrementButton.HeightRequest = StepButtonHeight * 2;

        var grid = new Grid
        {
            //BackgroundColor = Colors.Yellow,
            //BackgroundColor = Colors.Transparent,
            Padding = 0,
            Margin = new Thickness(5, 0, 0, 0),
            ColumnSpacing = 0,
            RowSpacing = 0,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            //HeightRequest = StepButtonHeight,
            RowDefinitions = { new RowDefinition(StepButtonHeight) },
            ColumnDefinitions = { new ColumnDefinition(StepButtonWidth), new ColumnDefinition(StepButtonWidth) }
        };

        grid.Add(decrementButton, 0, 0);
        grid.Add(incrementButton, 1, 0);
        return grid;
    }

    private View BuildVerticalStepper()
    {
        incrementButton = CreateStepButton(IncrementText, IncrementCommand, "Increase value");
        decrementButton = CreateStepButton(DecrementText, DecrementCommand, "Decrease value");
        incrementButton.HeightRequest = StepButtonHeight;
        decrementButton.HeightRequest = StepButtonHeight;
        var view = new VerticalStackLayout
        {
            Padding = 0,
            Spacing = 1,
            Margin = new Thickness(5, 0, 0, 0),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = StepButtonWidth + 1
        };

        view.Add(incrementButton);
        view.Add(decrementButton);
        return view;
    }


    private Button CreateStepButton(string text, ICommand command, string semanticDescription)
    {
        var button = new Button
        {
            Text = text,
            Command = command,
            Padding = 0,
            Margin = 0,
            BorderWidth = 0,
            CornerRadius = 0,
            FontSize = StepButtonFontSize,
            BackgroundColor = StepButtonBackgroundColor,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        button.SetBinding(Button.TextColorProperty, new Binding(nameof(TextColor), source: this));

        SemanticProperties.SetDescription(button, semanticDescription);

        return button;
    }

    private BoxView CreateVerticalDivider()
    {
        var divider = new BoxView { WidthRequest = 1, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, Opacity = 0.55 };

        divider.SetBinding(
            BoxView.ColorProperty,
            new Binding(nameof(BorderColor), source: this));

        return divider;
    }

    private BoxView CreateHorizontalDivider()
    {
        var divider = new BoxView { HeightRequest = 1, HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill, Opacity = 0.55 };

        divider.SetBinding(
            BoxView.ColorProperty,
            new Binding(nameof(BorderColor), source: this));

        return divider;
    }

    internal void UpdateStepperAppearance()
    {
        if (incrementButton is not null)
        {
            incrementButton.Text = IncrementText;
            incrementButton.FontSize = StepButtonFontSize;
            incrementButton.BackgroundColor = StepButtonBackgroundColor;
        }

        if (decrementButton is not null)
        {
            decrementButton.Text = DecrementText;
            decrementButton.FontSize = StepButtonFontSize;
            decrementButton.BackgroundColor = StepButtonBackgroundColor;
        }
    }

    internal void UpdateStepperVisibility()
    {
        stepperAttachment.IsVisible = ShowStepperButtons;
    }

    private void EnsureStepperAttachment()
    {
        if (ensuringStepperAttachment)
        {
            return;
        }

        ensuringStepperAttachment = true;

        try
        {
            // Use the inherited collection without assigning a replacement collection.
            // Depending on UraniumUI version and template state, this can be the backing
            // collection or the rendered EndIconsContainer.Children collection.
            var attachments = Attachments;

            if (attachments?.Contains(stepperAttachment) == true)
            {
                return;
            }

            if (attachments is not null && !attachments.IsReadOnly)
            {
                attachments.Add(stepperAttachment);
                return;
            }

            // Compatibility fallback for UraniumUI versions where Attachments exposes
            // a read-only collection but the protected rendered container is available.
            var container = endIconsContainer;

            if (container is not null &&
                !container.Children.Contains(stepperAttachment))
            {
                container.Children.Add(stepperAttachment);
            }
        }
        finally
        {
            ensuringStepperAttachment = false;
        }
    }
}
