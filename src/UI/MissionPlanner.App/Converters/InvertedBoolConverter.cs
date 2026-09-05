using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace MissionPlanner.App.Converters;

public sealed class InvertedBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : AvaloniaProperty.UnsetValue;
    }
}