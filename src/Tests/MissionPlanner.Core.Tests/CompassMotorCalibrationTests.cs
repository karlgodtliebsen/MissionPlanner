using FluentAssertions;
using MissionPlanner.Core.Setup.OptionalHardware;

namespace MissionPlanner.Core.Tests;

public sealed class CompassMotorCalibrationTests
{
    [Fact]
    public void InitialStateFailsClosedUntilExplicitStart()
    {
        CompassMotorCalibrationSnapshot.Initial.State.Should().Be(CompassMotorCalibrationState.Idle);
        CompassMotorCalibrationSnapshot.Initial.Samples.Should().BeEmpty();
    }

    [Fact]
    public void SamplePreservesStructuredProtocolValues()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var sample = new CompassMotorCalibrationSample(42.5, 8.2, 17, 1.1, 2.2, 3.3, timestamp);
        sample.Timestamp.Should().Be(timestamp);
        sample.InterferencePercent.Should().Be(17);
    }
}
