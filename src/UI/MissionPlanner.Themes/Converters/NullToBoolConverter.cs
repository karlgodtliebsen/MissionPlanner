using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace MissionPlanner.Themes.Converters;

/// <summary>Converts to true when none of the bound values is null.</summary>
public sealed class NullToBoolConverter : IMultiValueConverter
{
    /// <summary>Returns false when any bound value is null; otherwise true.</summary>
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 0)
        {
            return BindingOperations.DoNothing;
        }

        foreach (var value in values)
        {
            if (value is null)
            {
                return false;
            }
        }

        return true;
    }
}