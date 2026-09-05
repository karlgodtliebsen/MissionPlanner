using System.Globalization;
using Avalonia.Data.Converters;

namespace MissionPlanner.App.Converters;

/// <summary>
/// Converts between integer and double values.
/// </summary>
public sealed class IntegerDoubleConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int integer ? integer : 0d;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is double number
            ? checked((int)Math.Round(number, MidpointRounding.AwayFromZero))
            : 0;
    }
}
/// <summary>
/// Converts between string and double values.
/// </summary>
public sealed class StringDoubleConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var result = value is string str && double.TryParse(str, out var d) ? d : 0d;
        return result;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Converter.ConvertBack(value, targetType, parameter, culture);
    }
}

public static class Converter
{
    public static string ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return "";
        }
        {
            if (value is decimal d)
            {
                return d.ToString(culture);
            }
        }

        {
            if (value is double d)
            {
                return d.ToString(culture);
            }
        }
        {
            if (value is Int128 d)
            {
                return d.ToString(culture);
            }
        }

        {
            if (value is long d)
            {
                return d.ToString(culture);
            }
        }
        {
            if (value is int d)
            {
                return d.ToString(culture);
            }
        }
        {
            if (value is short d)
            {
                return d.ToString(culture);
            }
        }
        {
            if (value is UInt128 d)
            {
                return d.ToString(culture);
            }
        }
        return "";
    }
}


/// <summary>
/// Converts between string and integer values.
/// </summary>
public sealed class StringIntegerConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var result = value is string str && int.TryParse(str, out var d) ? d : 0d;
        return result;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var v = Converter.ConvertBack(value, targetType, parameter, culture);
        return int.TryParse(v, out var result) ? result : 0;
    }
}
