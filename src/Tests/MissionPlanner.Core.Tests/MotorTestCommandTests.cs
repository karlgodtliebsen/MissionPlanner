using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;
using MavParamType = MissionPlanner.MavLink.Parameters.MavParamType;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies sequence semantics at the encoder boundary without sending any packets.</summary>
public sealed class MotorTestCommandTests
{
    /// <summary>ArduPilot motor-test parameter one is sequence order, regardless of logical/output numbering.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public async Task LayoutTestSequenceReachesEncoderUnchanged(int frameType)
    {
        var id = new VehicleId(1, 1);
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
            VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null);
        var active = Substitute.For<IActiveVehicleContext>();
        active.VehicleId.Returns(id);
        active.State.Returns(state);
        active.IsOnline.Returns(true);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(id).Returns(new VehicleSession(state, new TransportEndPoint("test-only"), clock));
        var encoder = Substitute.For<IMavLinkCommandEncoder>();
        float[]? encoded = null;
        encoder.EncodeCommandLong(id.SystemId, id.ComponentId, (ushort)MavCmd.DoMotorTest, Arg.Any<IReadOnlyList<float>>()).Returns(call =>
        {
            encoded = call.Arg<IReadOnlyList<float>>().ToArray();
            throw new InvalidOperationException("Test capture: do not send a packet.");
        });
        var connection = Substitute.For<IVehicleConnectionSession>();
        using var service = new ActuatorTestService(active, registry, Substitute.For<IEventHub>(), connection,
            encoder, new VehicleOperationGate(), new VehicleParameterRegistry(), clock, NullLogger<ActuatorTestService>.Instance);
        var layout = new MotorLayoutResolver().Resolve(new Dictionary<string, VehicleParameter>
        {
            ["FRAME_CLASS"] = new("FRAME_CLASS", 1, MavParamType.Int32, 0, 2),
            ["FRAME_TYPE"] = new("FRAME_TYPE", frameType, MavParamType.Int32, 1, 2)
        })!;
        foreach (var motor in layout.Motors)
        {
            await service.TestMotorAsync(id, new MotorTestRequest(motor.TestOrder, MotorThrottleType.Percent, 10, 2), TestContext.Current.CancellationToken);
            Assert.Equal(new float[] { motor.TestOrder, 0, 10, 2, 1, 2, 0 }, encoded);
        }
        Assert.Empty(connection.ReceivedCalls());
    }
}
