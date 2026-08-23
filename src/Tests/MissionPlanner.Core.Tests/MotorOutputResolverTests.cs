using FluentAssertions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies physical servo-output resolution for ArduPilot logical motors.</summary>
public sealed class MotorOutputResolverTests
{
    private static readonly VehicleId vehicleId = new(1, 1);

    /// <summary>Verifies non-sequential physical outputs resolve to their assigned logical motors.</summary>
    [Fact]
    public void ResolvesSuppliedPhysicalOutputMapping()
    {
        var resolver = CreateResolver((1, 34), (2, 35), (3, 36), (4, 33));

        resolver.Resolve(vehicleId, 1).OutputChannel.Should().Be(4);
        resolver.Resolve(vehicleId, 2).OutputChannel.Should().Be(1);
        resolver.Resolve(vehicleId, 3).OutputChannel.Should().Be(2);
        resolver.Resolve(vehicleId, 4).OutputChannel.Should().Be(3);
    }

    /// <summary>Verifies joining Quad X test order with physical assignment keeps all three concepts distinct.</summary>
    [Fact]
    public void JoinsQuadXTestOrderWithoutChangingMotorTestSemantics()
    {
        var resolver = CreateResolver((1, 34), (2, 35), (3, 36), (4, 33));
        var layout = new MotorLayoutResolver().Resolve(new Dictionary<string, VehicleParameter>
        {
            ["FRAME_CLASS"] = Parameter("FRAME_CLASS", 1),
            ["FRAME_TYPE"] = Parameter("FRAME_TYPE", 1)
        })!;

        layout.Motors.OrderBy(motor => motor.TestOrder)
            .Select(motor => (motor.TestOrder, motor.MotorNumber, resolver.Resolve(vehicleId, motor.MotorNumber).OutputChannel))
            .Should().Equal((1, 1, 4), (2, 4, 3), (3, 2, 1), (4, 3, 2));
    }

    /// <summary>Verifies missing and non-motor functions do not create inferred assignments.</summary>
    [Fact]
    public void MissingAndNonMotorFunctionsAreNotAssigned()
    {
        var resolver = CreateResolver((3, 33), (5, 0), (6, 51));

        var result = resolver.Resolve(vehicleId, 3);

        result.Status.Should().Be(MotorOutputResolutionStatus.NotAssigned);
        result.OutputChannel.Should().BeNull();
        result.OutputChannels.Should().BeEmpty();
    }

    /// <summary>Verifies each resolution reads current registry state rather than a stale cache.</summary>
    [Fact]
    public void ChangedAssignmentIsReflectedImmediately()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, 4, 33);
        var resolver = new MotorOutputResolver(registry);
        resolver.Resolve(vehicleId, 1).OutputChannel.Should().Be(4);

        Store(registry, 4, 0);
        Store(registry, 6, 33);

        resolver.Resolve(vehicleId, 1).OutputChannel.Should().Be(6);
    }

    /// <summary>Verifies duplicate assignments are reported without selecting an arbitrary output.</summary>
    [Fact]
    public void DuplicateAssignmentIsAmbiguous()
    {
        var resolver = CreateResolver((2, 33), (7, 33));

        var result = resolver.Resolve(vehicleId, 1);

        result.Status.Should().Be(MotorOutputResolutionStatus.Ambiguous);
        result.OutputChannel.Should().BeNull();
        result.OutputChannels.Should().Equal(2, 7);
    }

    /// <summary>Verifies the non-contiguous ArduPilot function ranges for higher motors.</summary>
    [Theory]
    [InlineData(9, 82)]
    [InlineData(13, 160)]
    [InlineData(32, 179)]
    public void ResolvesHigherMotorFunctionRanges(int motorNumber, int functionValue)
    {
        CreateResolver((8, functionValue)).Resolve(vehicleId, motorNumber).OutputChannel.Should().Be(8);
    }

    private static MotorOutputResolver CreateResolver(params (int Channel, int Function)[] assignments)
    {
        var registry = new VehicleParameterRegistry();
        foreach (var (channel, function) in assignments)
        {
            Store(registry, channel, function);
        }

        return new MotorOutputResolver(registry);
    }

    private static void Store(VehicleParameterRegistry registry, int channel, int function)
    {
        registry.StoreParameter(
            vehicleId,
            Parameter($"SERVO{channel}_FUNCTION", function),
            CancellationToken.None);
    }

    private static VehicleParameter Parameter(string name, float value)
    {
        return new VehicleParameter(name, value, MavParamType.Int16, 0, 1);
    }
}
