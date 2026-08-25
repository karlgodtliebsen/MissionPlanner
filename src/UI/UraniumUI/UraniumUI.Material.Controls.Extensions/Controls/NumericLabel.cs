#nullable enable

using System.Globalization;

namespace UraniumUI.Material.Controls;

/// <summary>A label that formats a numeric value using a culture and numeric representation.</summary>
public partial class NumericLabel : Label
{
    private bool correctingRange;

    /// <summary>Initializes a numeric label.</summary>
    public NumericLabel()
    {
        FormatValue();
    }

    /// <summary>Occurs after <see cref="Value"/> materially changes.</summary>
    public event EventHandler<NumericValueChangedEventArgs>? ValueChanged;

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
        FormatValue();
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
                FormatValue();
            }
        }
        finally
        {
            correctingRange = false;
        }
    }

    internal void OnNumericTypeChanged() => OnRangeChanged();

    internal void FormatValue()
    {
        var rules = NumericTypeRules.Resolve(NumericType);
        var format = rules.IsInteger || string.IsNullOrWhiteSpace(NumberFormat) ? "G15" : NumberFormat;
        try
        {
            Text = Value.ToString(format, ResolveCulture());
        }
        catch (FormatException)
        {
            Text = Value.ToString("G15", ResolveCulture());
        }
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
}
