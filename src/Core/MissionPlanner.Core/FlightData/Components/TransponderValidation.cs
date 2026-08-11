namespace MissionPlanner.Core.FlightData.Components;

/// <summary>Validates human-entered four-digit octal squawk values.</summary>
public static class TransponderValidation
{
    /// <summary>Returns whether text contains exactly four octal digits.</summary>
    public static bool IsSquawk(string? value)
    {
        return value is { Length: 4 } && value.All(c => c is >= '0' and <= '7');
    }
}
