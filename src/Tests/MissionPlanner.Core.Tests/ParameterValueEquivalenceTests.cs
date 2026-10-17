using System.Globalization;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Comparison;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Tests;

/// <summary>Checks float readback noise consistently across dirty detection and comparison.</summary>
public sealed class ParameterValueEquivalenceTests
{
    /// <summary>Decimal values remain unchanged after REAL32 round trips, even at step midpoints.</summary>
    [Theory]
    [InlineData(0.35, 0.1)]
    [InlineData(-0.35, 0.1)]
    [InlineData(1.1, 0.01)]
    [InlineData(0.000035, 0.00001)]
    public void DecimalAndReadbackDoNotCreateDifferences(double value, double increment)
    {
        var metadata = ParameterFieldMetadata.Empty with { Increment = increment };
        var field = new ParameterEditField("GAIN", MavParamType.Real32, value, (float)value, value, metadata, null);
        Assert.False(field.IsModified);
        var service = new ParameterComparisonService(ParameterValueEquivalence.Default);
        var source = new ParameterComparisonSource("Test", "Test", DateTimeOffset.UtcNow, null);
        var result = service.Compare(source,
            new Dictionary<string, ParameterComparisonInput> { ["GAIN"] = new("GAIN", ((double)(float)value).ToString("R", CultureInfo.InvariantCulture)) },
            source,
            new Dictionary<string, ParameterComparisonInput> { ["GAIN"] = new("GAIN", value.ToString("R", CultureInfo.InvariantCulture)) },
            new Dictionary<string, ParameterFieldMetadata> { ["GAIN"] = metadata });
        Assert.Equal(ParameterComparisonStatus.Equal, Assert.Single(result.Rows).Status);
        Assert.False(result.Rows[0].CanStage);
        Assert.False(ParameterValueEquivalence.Default.AreEquivalent((float)value, value + increment, metadata));
    }
}
