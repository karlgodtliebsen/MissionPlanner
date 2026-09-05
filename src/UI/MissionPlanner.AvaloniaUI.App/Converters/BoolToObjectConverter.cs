using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace MissionPlanner.AvaloniaUI.App.Converters;

public sealed class BoolToObjectConverter : IValueConverter
{
    public object? TrueObject
    {
        get; set;
    }
    public object? FalseObject
    {
        get; set;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b
            ? (b ? TrueObject : FalseObject)
            : AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Equals(value, TrueObject) ? true : Equals(value, FalseObject) ? false : AvaloniaProperty.UnsetValue;
    }
}