using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using NSubstitute;
using MavParamType = MissionPlanner.MavLink.Parameters.MavParamType;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies bounded, safety-gated motor testing and servo output configuration.</summary>
public sealed class ActuatorSetupTests
{
    private static readonly VehicleId vehicleId = new(1, 1);
    private static readonly TransportEndPoint endPoint = new("test");

    /// <summary>Verifies only rotorcraft-style families expose motor testing.</summary>
    [Fact]
    public void MotorTestVisibilityFollowsVehicleFamily()
    {
        using var fixture = CreateFixture();
        fixture.Service.SupportsMotorTest(FirmwareFamily.ArduCopter).Should().BeTrue();
        fixture.Service.SupportsMotorTest(FirmwareFamily.Rover).Should().BeTrue();
        fixture.Service.SupportsMotorTest(FirmwareFamily.ArduPlane).Should().BeFalse();
    }

    /// <summary>Verifies bounds and armed-state safety reject a test before any command is sent.</summary>
    [Fact]
    public async Task BoundsAndArmedStateRejectMotorTest()
    {
        using var disarmed = CreateFixture();
        (await disarmed.Service.TestMotorAsync(vehicleId, new MotorTestRequest(1, MotorThrottleType.Percent, 10, 0), TestContext.Current.CancellationToken)).Success.Should().BeFalse();
        (await disarmed.Service.TestMotorAsync(vehicleId, new MotorTestRequest(1, MotorThrottleType.Percent, 10, 999), TestContext.Current.CancellationToken)).Success.Should().BeFalse();
        (await disarmed.Service.TestMotorAsync(vehicleId, new MotorTestRequest(1, MotorThrottleType.Percent, 500, 2), TestContext.Current.CancellationToken)).Success.Should().BeFalse();
        disarmed.CommandParameters.Should().BeEmpty("no command may be sent for a rejected test");

        using var armed = CreateFixture(armed: true);
        var result = await armed.Service.TestMotorAsync(vehicleId, new MotorTestRequest(1, MotorThrottleType.Percent, 10, 2), TestContext.Current.CancellationToken);
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Disarm");
    }

    /// <summary>Verifies an accepted test runs and emergency stop halts output.</summary>
    [Fact]
    public async Task AcceptedTestRunsAndEmergencyStopHalts()
    {
        using var fixture = CreateFixture();
        var test = fixture.Service.TestMotorAsync(vehicleId, new MotorTestRequest(2, MotorThrottleType.Percent, 15, 5), TestContext.Current.CancellationToken);
        await fixture.CommandSent.Task;
        await fixture.PublishAckAsync(MavResult.Accepted);
        var result = await test;

        result.Success.Should().BeTrue();
        fixture.Service.Current.State.Should().Be(MotorTestState.Running);
        fixture.Service.Current.ActiveMotor.Should().Be(2);

        await fixture.Service.EmergencyStopAsync(TestContext.Current.CancellationToken);
        fixture.Service.Current.State.Should().Be(MotorTestState.Stopped);
        fixture.CommandParameters.Last()[2].Should().Be(0, "the stop command drives zero throttle");
    }

    /// <summary>Verifies a rejected acknowledgement fails the test.</summary>
    [Fact]
    public async Task RejectedAcknowledgementFailsTest()
    {
        using var fixture = CreateFixture();
        var test = fixture.Service.TestMotorAsync(vehicleId, new MotorTestRequest(1, MotorThrottleType.Percent, 10, 2), TestContext.Current.CancellationToken);
        await fixture.CommandSent.Task;
        await fixture.PublishAckAsync(MavResult.Denied);
        var result = await test;

        result.Success.Should().BeFalse();
        fixture.Service.Current.State.Should().Be(MotorTestState.Failed);
    }

    /// <summary>Verifies ESC calibration guidance reflects the configured output protocol.</summary>
    [Fact]
    public void EscGuidanceReflectsProtocol()
    {
        using var analog = CreateFixture(pwmType: 0);
        analog.Service.GetEscCalibrationGuidance(vehicleId).Applicable.Should().BeTrue();

        using var digital = CreateFixture(pwmType: 6);
        var guidance = digital.Service.GetEscCalibrationGuidance(vehicleId);
        guidance.Applicable.Should().BeFalse();
        guidance.Steps.Should().BeEmpty();
    }

    /// <summary>Verifies servo outputs are discovered sparsely and functions are written on confirmation.</summary>
    [Fact]
    public async Task ServoOutputsDiscoverSparselyAndWriteFunctions()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "SERVO1_FUNCTION", 33);
        Store(registry, "SERVO3_FUNCTION", 35); // Sparse: output 2 absent.
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(State(now: now));
        var service = CreateServoService(context, registry, now);

        var configuration = await service.GetConfigurationAsync(vehicleId, TestContext.Current.CancellationToken);
        configuration.Outputs.Select(output => output.Output).Should().Equal(1, 3);

        var result = await service.SetFunctionAsync(vehicleId, 1, 36, TestContext.Current.CancellationToken);
        result.Success.Should().BeTrue();
        registry.GetParameter(vehicleId, "SERVO1_FUNCTION")!.Value.Should().Be(36);
    }

    private static ServoOutputConfigurationService CreateServoService(TestActiveVehicleContext context, VehicleParameterRegistry registry, DateTimeOffset now)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var metadata = Substitute.For<IVehicleParameterMetadataService>();
        metadata.GetAllMetadataAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(new Dictionary<string, ParameterMetadata>());
        var parameterService = Substitute.For<IVehicleParameterService>();
        parameterService.SetParameterAsync(vehicleId, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                registry.StoreParameter(vehicleId, new VehicleParameter(call.ArgAt<string>(1), call.ArgAt<float>(2), MavParamType.Int16, 0, 1), CancellationToken.None);
                return Task.FromResult(true);
            });
        return new ServoOutputConfigurationService(context, registry, metadata, parameterService, clock,
            Substitute.For<ILogger<ServoOutputConfigurationService>>());
    }

    private static void Store(VehicleParameterRegistry registry, string name, float value) =>
        registry.StoreParameter(vehicleId, new VehicleParameter(name, value, MavParamType.Int16, 0, 1), CancellationToken.None);

    private static ActuatorFixture CreateFixture(bool armed = false, int pwmType = 0)
    {
        var now = DateTimeOffset.UtcNow;
        var state = State(armed, now);
        var context = new TestActiveVehicleContext(state);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var registry = new VehicleParameterRegistry();
        registry.StoreParameter(vehicleId, new VehicleParameter("MOT_PWM_TYPE", pwmType, MavParamType.Int8, 0, 1), CancellationToken.None);
        var vehicleRegistry = Substitute.For<IVehicleRegistry>();
        vehicleRegistry.GetRequired(vehicleId).Returns(new VehicleSession(state, endPoint, clock));
        var eventHub = new EventHub(Substitute.For<ILogger<EventHub>>());
        var connection = Substitute.For<IMavLinkConnection>();
        var commandSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var captured = new List<IReadOnlyList<float>>();
        var encoder = Substitute.For<IMavLinkCommandEncoder>();
        encoder.EncodeCommandLong(Arg.Any<byte>(), Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<IReadOnlyList<float>>())
            .Returns(call =>
            {
                captured.Add(call.ArgAt<IReadOnlyList<float>>(3));
                commandSent.TrySetResult();
                return [1, 2, 3];
            });
        var service = new ActuatorTestService(context, vehicleRegistry, eventHub, connection, encoder,
            new VehicleOperationGate(), registry, clock, Substitute.For<ILogger<ActuatorTestService>>());
        return new ActuatorFixture(service, eventHub, commandSent, captured);
    }

    private static VehicleState State(bool armed = false, DateTimeOffset? now = null)
    {
        var timestamp = now ?? DateTimeOffset.UtcNow;
        var state = new VehicleState(vehicleId, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, timestamp,
            VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null) with
        {
            Flight = new VehicleFlightState(0, 0, 4, VehicleMode.Stabilize, armed,
                LandedState: VehicleLandedState.OnGround, ObservedAt: timestamp)
        };
        return state with
        {
            Radio = VehicleRadioState.Empty with { ServoOutputsRaw = [1500, 1500, 1500], ServoObservedAt = timestamp }
        };
    }

    private sealed record ActuatorFixture(
        ActuatorTestService Service,
        EventHub EventHub,
        TaskCompletionSource CommandSent,
        List<IReadOnlyList<float>> CommandParameters) : IDisposable
    {
        public Task PublishAckAsync(MavResult result) => EventHub.PublishAsync<MavLinkMessage>(
            MavLinkEventTopics.ReceivedMessage,
            new CommandAckMessage(1, 1, endPoint, (ushort)MavCmd.DoMotorTest, (byte)result, DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken);

        public void Dispose() => Service.Dispose();
    }

    private sealed class TestActiveVehicleContext(VehicleState state) : IActiveVehicleContext
    {
        private readonly CancellationTokenSource lifetime = new();

        public ActiveVehicleSnapshot Current { get; } = new(state.VehicleId, state);

        public VehicleId? VehicleId => Current.VehicleId;

        public VehicleState? State => Current.State;

        public bool IsOnline => Current.IsOnline;

        public CancellationToken ConnectionCancellationToken => lifetime.Token;

        public event EventHandler<ActiveVehicleChangedEventArgs>? Changed;
    }
}
