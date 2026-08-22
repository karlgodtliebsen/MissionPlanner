using FluentAssertions;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies frame-aware logical motor and motor-test ordering.</summary>
public sealed class MotorLayoutResolverTests
{
    /// <summary>Verifies Quad X preserves logical number, test order, rotation, and geometry.</summary>
    [Fact]
    public void QuadXResolvesArduPilotMotorSemantics()
    {
        var layout = Resolve(1, 1);

        layout.Motors.Should().BeEquivalentTo(
        [
            new MotorLayoutMotor(1, 1, MotorRotation.CounterClockwise, -.5, .5),
            new MotorLayoutMotor(2, 3, MotorRotation.CounterClockwise, .5, -.5),
            new MotorLayoutMotor(3, 4, MotorRotation.Clockwise, .5, .5),
            new MotorLayoutMotor(4, 2, MotorRotation.Clockwise, -.5, -.5)
        ]);
    }

    /// <summary>Verifies Quad X presentation follows test order rather than logical motor number.</summary>
    [Fact]
    public void QuadXPresentationOrderIsA1B4C2D3()
    {
        Resolve(1, 1).Motors
            .OrderBy(motor => motor.TestOrder)
            .Select(motor => motor.Label)
            .Should().Equal(
                "Test A — Motor 1 — CCW",
                "Test B — Motor 4 — CW",
                "Test C — Motor 2 — CCW",
                "Test D — Motor 3 — CW");
    }

    /// <summary>Verifies a non-Quad-X layout uses its own ArduPilot mapping.</summary>
    [Fact]
    public void QuadPlusRetainsDistinctMapping()
    {
        Resolve(1, 0).Motors
            .OrderBy(motor => motor.TestOrder)
            .Select(motor => motor.MotorNumber)
            .Should().Equal(3, 1, 4, 2);
    }

    /// <summary>Verifies previously supported larger frames remain available.</summary>
    [Theory]
    [InlineData(2, 0, 6)]
    [InlineData(3, 0, 8)]
    [InlineData(12, 0, 12)]
    public void ResolvesSupportedMatrixCounts(int frameClass, int frameType, int count)
    {
        Resolve(frameClass, frameType).Motors.Should().HaveCount(count);
    }

    /// <summary>Verifies missing and unknown layouts fail closed.</summary>
    [Fact]
    public void MissingOrUnsupportedFrameFailsClosed()
    {
        var resolver = new MotorLayoutResolver();
        resolver.Resolve(new Dictionary<string, VehicleParameter>()).Should().BeNull();
        resolver.Resolve(Parameters(1, 999)).Should().BeNull();
    }

    private static MotorLayout Resolve(int frameClass, int frameType)
    {
        return new MotorLayoutResolver().Resolve(Parameters(frameClass, frameType))!;
    }

    private static IReadOnlyDictionary<string, VehicleParameter> Parameters(int frameClass, int frameType)
    {
        return new Dictionary<string, VehicleParameter>
        {
            ["FRAME_CLASS"] = new("FRAME_CLASS", frameClass, MavParamType.Int32, 0, 2),
            ["FRAME_TYPE"] = new("FRAME_TYPE", frameType, MavParamType.Int32, 1, 2)
        };
    }
}
