#nullable enable

using System.Globalization;

namespace UraniumUI.Material.Controls;

/// <summary>Provides MAV-compatible numeric behavior without depending on a MAVLink enum.</summary>
internal readonly record struct NumericTypeRules(bool IsInteger, bool IsUnsigned, double Min, double Max)
{
    /// <summary>Resolves a bound textual numeric type. Unknown values retain double behavior.</summary>
    public static NumericTypeRules Resolve(string? numericType)
    {
        return numericType?.Trim().ToUpperInvariant() switch
        {
            "UINT8" => new(true, true, byte.MinValue, byte.MaxValue),
            "INT8" => new(true, false, sbyte.MinValue, sbyte.MaxValue),
            "UINT16" => new(true, true, ushort.MinValue, ushort.MaxValue),
            "INT16" => new(true, false, short.MinValue, short.MaxValue),
            "UINT32" => new(true, true, uint.MinValue, uint.MaxValue),
            "INT32" => new(true, false, int.MinValue, int.MaxValue),
            _ => new(false, false, double.MinValue, double.MaxValue)
        };
    }

    /// <summary>Gets parsing styles appropriate for this numeric type.</summary>
    public NumberStyles GetNumberStyles(bool allowSign, bool allowThousands)
    {
        var styles = IsInteger ? NumberStyles.None : NumberStyles.AllowDecimalPoint;
        if (allowSign && !IsUnsigned)
        {
            styles |= NumberStyles.AllowLeadingSign;
        }

        if (allowThousands && !IsInteger)
        {
            styles |= NumberStyles.AllowThousands;
        }

        return styles;
    }

    /// <summary>Normalizes a numeric value to the selected representation.</summary>
    public double Normalize(double value) => IsInteger
        ? Math.Round(value, MidpointRounding.AwayFromZero)
        : value;
}
