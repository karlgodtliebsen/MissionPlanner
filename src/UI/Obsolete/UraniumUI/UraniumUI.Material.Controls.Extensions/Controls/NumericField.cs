#nullable enable

using System.Globalization;

namespace UraniumUI.Material.Controls;

/// <summary>
/// A culture-aware Material numeric text field that separates temporary editing text from
/// its numeric <see cref="Value"/> and formats only when editing is committed.
/// </summary>
/// <remarks>
/// Bind <see cref="Value"/> to a numeric ViewModel property. Do not apply StringFormat to
/// the inherited <see cref="TextField.Text"/> binding; use <see cref="NumberFormat"/>.
/// </remarks>
public partial class NumericField : TextField
{
    private bool updatingText;
    private bool updatingValue;
    private bool correctingRange;

    /// <summary>Initializes a numeric Material field.</summary>
    public NumericField()
    {
        Keyboard = Keyboard.Numeric;
        TextChanged += OnNumericTextChanged;
        Completed += OnCompleted;
        EntryView.Unfocused += OnUnfocused;
        FormatValue();
    }

    /// <summary>Occurs after <see cref="Value"/> materially changes.</summary>
    public event EventHandler<NumericValueChangedEventArgs>? ValueChanged;

    private void OnNumericTextChanged(object? sender, TextChangedEventArgs args)
    {
        if (updatingText || string.Equals(args.OldTextValue, args.NewTextValue, StringComparison.Ordinal))
        {
            return;
        }

        if (!IsPotentialNumber(args.NewTextValue))
        {
            RestoreText(args.OldTextValue);
            return;
        }

        if (!TryParse(args.NewTextValue, out var parsed))
        {
            SetValidity(false);
            return;
        }

        var inRange = parsed >= EffectiveMin && parsed <= EffectiveMax;
        SetValidity(inRange);
        if (!inRange || Value.Equals(parsed))
        {
            return;
        }

        updatingValue = true;
        try
        {
            Value = parsed;
        }
        finally
        {
            updatingValue = false;
        }
    }

    private void OnCompleted(object? sender, EventArgs args)
    {
        Commit();
    }

    private void OnUnfocused(object? sender, FocusEventArgs args)
    {
        Commit();
    }

    private void Commit()
    {
        if (!TryParse(Text, out var parsed))
        {
            FormatValue();
            return;
        }

        if (ClampOnCommit)
        {
            parsed = Math.Clamp(parsed, EffectiveMin, EffectiveMax);
        }
        else if (parsed < EffectiveMin || parsed > EffectiveMax)
        {
            SetValidity(false);
            return;
        }

        if (!Value.Equals(parsed))
        {
            Value = parsed;
        }

        FormatValue();
    }

    internal double CoerceValue(double value)
    {
        var rules = NumericTypeRules.Resolve(NumericType);
        value = rules.Normalize(value);
        return double.IsNaN(value)
            ? Value
            : double.IsPositiveInfinity(value)
                ? EffectiveMax
                : double.IsNegativeInfinity(value)
                    ? EffectiveMin
                    : Math.Clamp(value, EffectiveMin, EffectiveMax);
    }

    internal void OnValueChanged(double oldValue, double newValue)
    {
        if (!updatingValue && !EntryView.IsFocused)
        {
            FormatValue();
        }

        SetValidity(true);
        if (!oldValue.Equals(newValue))
        {
            ValueChanged?.Invoke(this, new NumericValueChangedEventArgs(oldValue, newValue));
        }
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

            var corrected = CoerceValue(Value);
            if (!Value.Equals(corrected))
            {
                Value = corrected;
            }
            else
            {
                FormatValueWhenNotEditing();
            }
        }
        finally
        {
            correctingRange = false;
        }
    }

    internal void OnNumericTypeChanged()
    {
        var rules = NumericTypeRules.Resolve(NumericType);
        AllowSign = !rules.IsUnsigned;
        AllowThousands = !rules.IsInteger;
        OnRangeChanged();
    }

    internal void FormatValueWhenNotEditing()
    {
        if (!EntryView.IsFocused)
        {
            FormatValue();
        }
    }

    private void FormatValue()
    {
        var format = string.IsNullOrWhiteSpace(NumberFormat) ? "G15" : NumberFormat;
        string formatted;
        try
        {
            formatted = Value.ToString(format, ResolveCulture());
        }
        catch (FormatException)
        {
            formatted = Value.ToString("G15", ResolveCulture());
        }

        if (string.Equals(Text, formatted, StringComparison.Ordinal))
        {
            SetValidity(true);
            return;
        }

        RestoreText(formatted);
        SetValidity(true);
    }

    private void RestoreText(string? text)
    {
        updatingText = true;
        try
        {
            Text = text ?? string.Empty;
            EntryView.CursorPosition = Text.Length;
        }
        finally
        {
            updatingText = false;
        }
    }

    private bool TryParse(string? text, out double value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = default;
            return false;
        }

        var rules = NumericTypeRules.Resolve(NumericType);
        return double.TryParse(text, rules.GetNumberStyles(AllowSign, AllowThousands), ResolveCulture(), out value) &&
               double.IsFinite(value) && value >= rules.Min && value <= rules.Max;
    }

    private bool IsPotentialNumber(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        var number = ResolveCulture().NumberFormat;
        var remaining = text;
        if (AllowSign && remaining.StartsWith(number.NegativeSign, StringComparison.Ordinal))
        {
            remaining = remaining[number.NegativeSign.Length..];
        }
        else if (AllowSign && remaining.StartsWith(number.PositiveSign, StringComparison.Ordinal))
        {
            remaining = remaining[number.PositiveSign.Length..];
        }

        if (remaining.Contains(number.NegativeSign, StringComparison.Ordinal) ||
            remaining.Contains(number.PositiveSign, StringComparison.Ordinal))
        {
            return false;
        }

        var rules = NumericTypeRules.Resolve(NumericType);
        if ((rules.IsInteger && remaining.Contains(number.NumberDecimalSeparator, StringComparison.Ordinal)) ||
            Count(remaining, number.NumberDecimalSeparator) > 1)
        {
            return false;
        }

        remaining = remaining.Replace(number.NumberDecimalSeparator, string.Empty, StringComparison.Ordinal);
        if (AllowThousands && !string.IsNullOrEmpty(number.NumberGroupSeparator))
        {
            remaining = remaining.Replace(number.NumberGroupSeparator, string.Empty, StringComparison.Ordinal);
        }

        return remaining.All(char.IsDigit);
    }

    private double EffectiveMin
    {
        get
        {
            var rules = NumericTypeRules.Resolve(NumericType);
            return Math.Clamp(Min, rules.Min, rules.Max);
        }
    }

    private double EffectiveMax
    {
        get
        {
            var rules = NumericTypeRules.Resolve(NumericType);
            return Math.Clamp(Math.Max(Max, EffectiveMin), rules.Min, rules.Max);
        }
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

    private void SetValidity(bool valid)
    {
        if (IsTextValid != valid)
        {
            SetValue(IsTextValidPropertyKey, valid);
        }
    }

    private static int Count(string value, string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return 0;
        }

        var count = 0;
        for (var index = 0; (index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0; index += token.Length)
        {
            count++;
        }

        return count;
    }
}
