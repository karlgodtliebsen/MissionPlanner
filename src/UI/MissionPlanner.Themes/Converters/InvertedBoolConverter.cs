using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace MissionPlanner.Themes.Converters;

/// <summary>Converts a boolean to its negation.</summary>
public sealed class InvertedBoolConverter : IValueConverter
{
    /// <summary>Returns the negation of a boolean value.</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : AvaloniaProperty.UnsetValue;
    }

    /// <summary>Returns the negation of a boolean value.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : AvaloniaProperty.UnsetValue;
    }
}