using FluentAssertions;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Tests;

public sealed class MotorLayoutResolverTests
{
    [Theory]
    [InlineData(1, 4)]
    [InlineData(2, 6)]
    [InlineData(3, 8)]
    [InlineData(12, 12)]
    public void ResolvesSupportedMatrixCounts(int frameClass, int count)
    {
        var values = new Dictionary<string, VehicleParameter> { { "FRAME_CLASS", new VehicleParameter("FRAME_CLASS", frameClass, MavParamType.Int32, 0, 1) } };
        new MotorLayoutResolver().Resolve(values)!.Motors.Should().HaveCount(count);
    }

    [Fact]
    public void MissingFrameFailsClosed()
    {
        new MotorLayoutResolver().Resolve(new Dictionary<string, VehicleParameter>()).Should().BeNull();
    }
}
