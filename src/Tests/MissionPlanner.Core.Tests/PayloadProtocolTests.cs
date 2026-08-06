using FluentAssertions;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.FlightData.Components;
using MissionPlanner.Core.FlightData.Payload;
using MissionPlanner.Core.Simulation.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies payload discovery and command bounds.</summary>
public sealed class PayloadProtocolTests
{
    /// <summary>Multiple cameras and gimbals remain separately selectable by component ID.</summary>
    [Fact]
    public void DiscoversMultipleExactPayloadComponents()
    {
        var registry = new VehicleComponentRegistry();
        var now = DateTimeOffset.UtcNow;
        registry.Observe(new HeartbeatMessage(1, 100, new TransportEndPoint("test"), 0, (byte)MavType.Camera, 0, 0, 0, 3, now));
        registry.Observe(new HeartbeatMessage(1, 101, new TransportEndPoint("test"), 0, (byte)MavType.Camera, 0, 0, 0, 3, now));
        registry.Observe(new HeartbeatMessage(1, 154, new TransportEndPoint("test"), 0, (byte)MavType.Gimbal, 0, 0, 0, 3, now));
        var service = CreateService(registry);

        service.GetCameras(1).Select(item => item.Component.Key.ComponentId).Should().Equal(100, 101);
        service.GetGimbals(1).Select(item => item.Component.Key.ComponentId).Should().Equal(154);
    }

    /// <summary>Unsafe gimbal angles are rejected before protocol access.</summary>
    [Fact]
    public async Task RejectsOutOfRangeGimbalAngles()
    {
        var service = CreateService(new VehicleComponentRegistry());
        var result = await service.SetPitchYawAsync(new(1, 1), 154, -91, 0, false, TestContext.Current.CancellationToken);
        result.Accepted.Should().BeFalse();
    }

    private static PayloadProtocolService CreateService(IVehicleComponentRegistry registry) => new(registry,
        Substitute.For<IVehicleRegistry>(), Substitute.For<IVehicleConnectionSession>(),
        Substitute.For<IMavLinkCommandEncoder>(), Substitute.For<ICommandAckTracker>(),
        Substitute.For<IVehicleOperationGate>());
}
