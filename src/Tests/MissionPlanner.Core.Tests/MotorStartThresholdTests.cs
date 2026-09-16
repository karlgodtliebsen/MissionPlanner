using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies user-observed threshold calculation and guarded writes.</summary>
public sealed class MotorStartThresholdTests
{
    /// <summary>The highest motor threshold controls the proposed armed and minimum output.</summary>
    [Fact]
    public void HighestThresholdProducesBenchRecommendation()
    {
        var recommendation = MotorStartThresholdService.Recommend([14, 14, 15, 14]);
        Assert.Equal(15, recommendation.HighestPercent);
        Assert.Equal(0.17, recommendation.SpinArm, 6);
        Assert.Equal(0.20, recommendation.SpinMin, 6);
    }

    /// <summary>Acknowledgement is required and cancellation never writes parameters.</summary>
    [Fact]
    public async Task AcknowledgementAndCancellationGateWrites()
    {
        using var fixture = new Fixture();
        Assert.Throws<InvalidOperationException>(() => fixture.Service.Start(false));
        fixture.Service.Start(true);
        await fixture.Service.CancelAsync(TestContext.Current.CancellationToken);
        Assert.False((await fixture.Service.ApplyAsync(false, TestContext.Current.CancellationToken)).Success);
        Assert.Empty(fixture.Writes);
    }

    /// <summary>A six-motor frame produces exactly six independently observed steps.</summary>
    [Fact]
    public void HexaProducesSixSteps()
    {
        using var fixture = new Fixture(2);
        fixture.Service.Start(true);
        Assert.Equal(6, fixture.Service.Measurements.Count);
    }

    /// <summary>Only a successful pulse can be confirmed, and accepted review writes the calculated pair.</summary>
    [Fact]
    public async Task ConfirmedMeasurementsWriteExpectedParameters()
    {
        using var fixture = new Fixture();
        fixture.Service.Start(true);
        Assert.Throws<InvalidOperationException>(() => fixture.Service.ConfirmReliableRotation());
        foreach (var threshold in new[] { 14, 14, 15, 14 })
        {
            while (fixture.Service.TestPercent < threshold)
            {
                fixture.Service.Increase();
            }
            await fixture.Service.PulseAsync(TestContext.Current.CancellationToken);
            fixture.Service.ConfirmReliableRotation();
        }
        Assert.True(fixture.Service.IsComplete);
        Assert.False((await fixture.Service.ApplyAsync(false, TestContext.Current.CancellationToken)).Success);
        Assert.Empty(fixture.Writes);
        Assert.True((await fixture.Service.ApplyAsync(true, TestContext.Current.CancellationToken)).Success);
        Assert.Contains(fixture.Writes, write => write.Name == "MOT_SPIN_ARM" && Math.Abs(write.Value - 0.17) < 0.0001);
        Assert.Contains(fixture.Writes, write => write.Name == "MOT_SPIN_MIN" && Math.Abs(write.Value - 0.20) < 0.0001);
        Assert.Equal("MOT_SPIN_MIN", fixture.Writes[0].Name);
    }

    /// <summary>Disconnected or replaced vehicles cannot display an earlier assistant session as current evidence.</summary>
    [Fact]
    public void VehicleBoundaryInvalidatesDisplayedSession()
    {
        using var fixture = new Fixture();
        fixture.Service.Start(true);
        Assert.True(fixture.Service.HasSession);
        fixture.Active.IsOnline.Returns(false);
        Assert.False(fixture.Service.HasSession);
        Assert.Empty(fixture.Service.Measurements);
        Assert.Null(fixture.Service.CurrentMotor);
        Assert.False(fixture.Service.IsComplete);
        fixture.Active.IsOnline.Returns(true);
        fixture.Active.VehicleId.Returns(new VehicleId(2, 1));
        Assert.False(fixture.Service.HasSession);
        Assert.Empty(fixture.Service.Measurements);
    }

    private sealed class Fixture : IDisposable
    {
        public IActiveVehicleContext Active { get; } = Substitute.For<IActiveVehicleContext>();
        public MotorStartThresholdService Service { get; }
        public List<(string Name, double Value)> Writes { get; } = [];

        public Fixture(int frameClass = 1)
        {
            var id = new VehicleId(1, 1);
            var parameters = new VehicleParameterRegistry();
            void Store(string name, float value) => parameters.StoreParameter(id, new VehicleParameter(name, value, MavParamType.Real32, 0, 1), CancellationToken.None);
            Store("FRAME_CLASS", frameClass);
            Store("FRAME_TYPE", 0);
            for (var motor = 1; motor <= 6; motor++)
            {
                Store($"SERVO{motor}_FUNCTION", 32 + motor);
            }
            Store("MOT_SPIN_ARM", 0.10f);
            Store("MOT_SPIN_MIN", 0.15f);
            var active = Active;
            active.VehicleId.Returns(id);
            active.IsOnline.Returns(true);
            active.State.Returns(new VehicleState(id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online,
                DateTimeOffset.UtcNow, VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null));
            var actuator = Substitute.For<IActuatorTestService>();
            actuator.MaximumThrottlePercent.Returns(25);
            actuator.TestMotorAsync(id, Arg.Any<MotorTestRequest>(), Arg.Any<CancellationToken>())
                .Returns(new MotorTestResult(true, "Pulse accepted"));
            var protocol = Substitute.For<IVehicleParameterService>();
            protocol.SetParameterAsync(id, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    var name = call.Arg<string>()!;
                    var value = call.Arg<float>();
                    Writes.Add((name, value));
                    Store(name, value);
                    return true;
                });
            var spin = new MotorSpinParameterService(parameters, Substitute.For<IVehicleParameterMetadataService>(), protocol);
            Service = new(active, parameters, new MotorLayoutResolver(), new MotorOutputResolver(parameters), actuator, spin, new VehicleOperationGate());
        }

        public void Dispose() => Service.Dispose();
    }
}
