using System.Globalization;
using Avalonia.Data.Converters;

namespace MissionPlanner.App.Views.Diagnostics;

/// <summary>Displays evidence timestamps in local time with their UTC offset.</summary>
public sealed class DiagnosticTimeConverter : IValueConverter
{
    /// <summary>Shared stateless converter.</summary>
    public static DiagnosticTimeConverter Instance { get; } = new();

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTimeOffset time ? time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff zzz", culture) : "Time unknown";

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("Evidence timestamps are read-only.");
}
