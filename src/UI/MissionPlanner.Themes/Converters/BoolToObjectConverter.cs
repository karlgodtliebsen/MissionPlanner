using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace MissionPlanner.Themes.Converters;

/// <summary>Converts a boolean to one of two configured objects.</summary>
public sealed class BoolToObjectConverter : IValueConverter
{
    /// <summary>Gets or sets the object returned for true.</summary>
    public object? TrueObject
    {
        get; set;
    }
    /// <summary>Gets or sets the object returned for false.</summary>
    public object? FalseObject
    {
        get; set;
    }

    /// <summary>Returns <see cref="TrueObject"/> or <see cref="FalseObject"/> for a boolean value.</summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b
            ? (b ? TrueObject : FalseObject)
            : AvaloniaProperty.UnsetValue;
    }

    /// <summary>Returns true or false when the value equals the true or false object.</summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Equals(value, TrueObject) ? true : Equals(value, FalseObject) ? false : AvaloniaProperty.UnsetValue;
    }
}