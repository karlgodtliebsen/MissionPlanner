using System.Globalization;
using MissionPlanner.Library.Math;

namespace MissionPlanner.Tests;

/// <summary>Guards decimal editor arithmetic against runtime conversion changes.</summary>
public sealed class MathUtilsTests
{
    /// <summary>Metadata steps retain decimal precision, including exponent notation.</summary>
    [Theory]
    [InlineData(0.01, "0.01")]
    [InlineData(-0.1, "-0.1")]
    [InlineData(1e-10, "0.0000000001")]
    public void DecimalConversionPreservesMetadataPrecision(double value, string expected)
    {
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), MathUtils.ToDecimal(value));
    }

    /// <summary>Invalid and oversized editor values retain the overflow contract.</summary>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.MaxValue)]
    public void DecimalConversionRejectsValuesOutsideItsRange(double value)
    {
        Assert.Throws<OverflowException>(() => MathUtils.ToDecimal(value));
    }

    /// <summary>A decimal midpoint rounds to the intended step instead of following binary noise.</summary>
    [Fact]
    public void StepEquivalenceRetainsDecimalMidpointRounding()
    {
        Assert.True(MathUtils.AreEquivalent(0.15, 0.2, 0.1));
        Assert.False(MathUtils.AreEquivalent(0.15, 0.1, 0.1));
    }

    /// <summary>REAL32 metadata must not expand a two-place step into a long decimal display.</summary>
    [Theory]
    [InlineData("en-US", "1.23")]
    [InlineData("da-DK", "1,23")]
    public void StepFormattingUsesMetadataPrecision(string culture, string expected)
    {
        Assert.Equal(expected, MathUtils.FormatAtStepPrecision(1.234567, 0.01f, CultureInfo.GetCultureInfo(culture)));
    }
}
