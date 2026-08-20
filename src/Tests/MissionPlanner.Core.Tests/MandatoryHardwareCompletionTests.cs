using FluentAssertions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies deterministic Mandatory Hardware definitions and tuning calculations.</summary>
public sealed class MandatoryHardwareCompletionTests
{
    /// <summary>Verifies the final workflow order remains aligned with the fixed TabView content.</summary>
    [Fact]
    public void CatalogUsesRequiredMandatoryHardwareOrder()
    {
        var catalog = new SetupWorkflowCatalog();

        catalog.Workflows.Select(workflow => workflow.Key).Should().Equal(
            SetupWorkflowKey.Frame,
            SetupWorkflowKey.Accelerometer,
            SetupWorkflowKey.Compass,
            SetupWorkflowKey.Radio,
            SetupWorkflowKey.ServoOutput,
            SetupWorkflowKey.Esc,
            SetupWorkflowKey.FlightModes,
            SetupWorkflowKey.FailSafe,
            SetupWorkflowKey.InitTuneParameters,
            SetupWorkflowKey.HwId,
            SetupWorkflowKey.Adsb);
    }

    /// <summary>Verifies the legacy nine-inch, four-cell recommendation remains stable.</summary>
    [Fact]
    public void CalculatorPreservesLegacyRecommendations()
    {
        var values = InitTuneParametersCalculator.Calculate(9, 4, 4.2, 3.3);

        values["ATC_ACCEL_Y_MAX"].Should().Be(27900);
        values["INS_GYRO_FILTER"].Should().Be(46);
        values["MOT_THST_EXPO"].Should().Be(0.58);
        values["MOT_BAT_VOLT_MAX"].Should().BeApproximately(16.8, 0.0001);
        values["MOT_BAT_VOLT_MIN"].Should().BeApproximately(13.2, 0.0001);
    }

    /// <summary>Verifies invalid physical inputs are rejected before recommendations are generated.</summary>
    [Theory]
    [InlineData(0, 4, 4.2, 3.3)]
    [InlineData(9, 0, 4.2, 3.3)]
    [InlineData(9, 4, 3.2, 3.3)]
    public void CalculatorRejectsInvalidInputs(double propeller, int cells, double maximum, double minimum)
    {
        var action = () => InitTuneParametersCalculator.Calculate(propeller, cells, maximum, minimum);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }
}
