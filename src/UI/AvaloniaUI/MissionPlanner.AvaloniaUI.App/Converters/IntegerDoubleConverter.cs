using System.Globalization;
using Avalonia.Data.Converters;

namespace MissionPlanner.AvaloniaUI.App.Converters;

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
