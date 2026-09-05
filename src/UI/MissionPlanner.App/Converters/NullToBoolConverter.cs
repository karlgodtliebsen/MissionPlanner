using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace MissionPlanner.App.Converters;

public sealed class NullToBoolConverter : IMultiValueConverter
{
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