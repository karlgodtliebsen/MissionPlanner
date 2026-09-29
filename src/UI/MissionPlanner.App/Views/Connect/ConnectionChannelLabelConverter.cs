using System.Globalization;
using Avalonia.Data.Converters;

namespace MissionPlanner.App.Views.Connect;

/// <summary>Displays readable transport names without changing persisted channel identifiers.</summary>
public sealed class ConnectionChannelLabelConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        "UDP" => "UDP Listen",
        "UDPCl" => "UDP Client",
        "WS" => "WebSocket",
        "WSS" => "WebSocket (TLS)",
        _ => value
    };

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
