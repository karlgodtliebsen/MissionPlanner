using System.Globalization;

namespace UraniumUI.Material.Controls.Converters;

/// <summary>
/// Converts between byte and double values.
/// </summary>
public sealed class ByteDoubleConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is byte v ? v : 0d;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is double number
            ? checked((byte)Math.Round(number, MidpointRounding.AwayFromZero))
            : 0;
    }
}
