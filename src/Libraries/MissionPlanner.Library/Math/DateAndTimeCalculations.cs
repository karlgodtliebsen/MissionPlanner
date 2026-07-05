using System.Diagnostics;

namespace Domain.Library.Math;

/// <summary>
/// Provides methods for performing date and time calculations.
/// </summary>
public static class DateAndTimeCalculations
{
    /// <summary>
    /// Calculates the quarter of the year for a given date.
    /// </summary>
    /// <param name="dateTime">The date for which to calculate the quarter.</param>
    /// <returns>The quarter of the year (1-4).</returns>
    public static int CalculateQuarter(System.DateTime dateTime)
    {
        var quarter = dateTime.Month switch
        {
            > 0 and <= 3 => 1,
            > 3 and <= 6 => 2,
            > 6 and <= 9 => 3,
            > 9 and <= 12 => 4,
            var _ => 0 // default value
        };
        Debug.WriteLine($"{dateTime:d} => Month={dateTime.Month} and Quarter=Q{quarter}");
        return quarter;
    }
}
