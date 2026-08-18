using FluentAssertions;
using MissionPlanner.App.Views.Common;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;
using MissionPlanner.Core.Setup.MandatoryHardware;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies structured radio-channel presentation without parsing display text.</summary>
public sealed class RadioChannelPresentationTests
{
    /// <summary>Verifies structured endpoint, dead-zone, role, and reversal fields reach the row model.</summary>
    [Fact]
    public void ChannelPresentationUsesStructuredValues()
    {
        var info = new RadioChannelInfo(6, 1480, -0.04, 980, 2020, 1510, true, "Roll", 35, RadioChannelKind.CenteredAxis);
        var row = new RadioChannelDisplayViewModel(info, false);

        row.Minimum.Should().Be(980);
        row.Maximum.Should().Be(2020);
        row.Trim.Should().Be(1510);
        row.DeadZone.Should().Be(35);
        row.IsReversed.Should().BeTrue();
        row.RoleLabel.Should().Be("ROLL");
        row.PresentationKind.Should().Be(RadioChannelPresentationKind.CenteredAxis);
    }

    /// <summary>Verifies calibration markers distinguish capture and Review data.</summary>
    [Fact]
    public void ReviewPresentationExposesFixedExtremaAndCandidateTrim()
    {
        var row = new RadioChannelDisplayViewModel(
            new RadioChannelInfo(1, 1505, 0, 1000, 2000, 1500, false, "Roll"),
            false);
        var capture = new RadioChannelCapture(1, 990, 2010, 1505) { CandidateTrim = 1505 };

        row.ApplyCalibration(capture, RadioCalibrationState.Review);

        row.CapturedMinimum.Should().Be(990);
        row.CapturedMaximum.Should().Be(2010);
        row.CandidateTrim.Should().Be(1505);
        row.ShowCapturedRange.Should().BeTrue();
    }

    /// <summary>Verifies live updates reuse the row object and retain structured configuration.</summary>
    [Fact]
    public void LiveUpdateMutatesExistingRowInPlace()
    {
        var row = new RadioChannelDisplayViewModel(
            new RadioChannelInfo(1, 1400, -0.2, 1000, 2000, 1500, false, "Roll", 30, RadioChannelKind.CenteredAxis),
            false);

        row.Update(new RadioChannelInfo(1, 1600, 0.2, 1000, 2000, 1500, false, "Roll", 30, RadioChannelKind.CenteredAxis), false);

        row.Pwm.Should().Be(1600);
        row.DeadZone.Should().Be(30);
    }

    /// <summary>Verifies intermediate auxiliary input is not mislabeled as a discrete switch state.</summary>
    [Theory]
    [InlineData(1000, "LOW")]
    [InlineData(1500, "MID")]
    [InlineData(2000, "HIGH")]
    [InlineData(1300, "Variable")]
    [InlineData(1700, "Variable")]
    public void AuxiliaryLabelsRemainHonest(int pwm, string expected)
    {
        RadioChannelDisplayViewModel.DescribeAuxiliary(pwm).Should().Be(expected);
    }
}
