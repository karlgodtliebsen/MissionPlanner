using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Commands;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class MassStorageRebootTests
{
    [Theory]
    [InlineData(false, false, VehicleCommandResult.Denied)]
    [InlineData(true, true, VehicleCommandResult.Denied)]
    [InlineData(false, true, VehicleCommandResult.Accepted)]
    public async Task RequiresDisarmedConfirmationAndEncodesMassStorageMode(bool armed, bool confirmed, VehicleCommandResult expected)
    {
        var now = DateTimeOffset.UtcNow;
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var state = new VehicleState(new(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
            VehicleMode.Unknown, armed, null, null, null, null, null, null, null, null);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(new(1, 1)).Returns(new VehicleSession(state, new("test"), clock));
        var protocol = Substitute.For<IMavLinkConnection>();
        var session = Substitute.For<IVehicleConnectionSession>();
        session.Connection.Returns(protocol);
        var encoder = Substitute.For<IMavLinkCommandEncoder>();
        var tracker = new CommandAckTracker();
        protocol.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            tracker.Handle(new CommandAckMessage(1, 1, new("test"), MavLinkCommandIds.PreflightRebootShutdown, 0, now));
            return ValueTask.CompletedTask;
        });
        var service = new VehicleCommandService(registry, Substitute.For<IDomainEventHub>(), session, encoder, tracker,
            clock, new VehicleCommandPolicy(clock), Substitute.For<IArduPilotModeCatalog>());
        var result = await service.RebootMassStorageAsync(new(1, 1), confirmed, TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.Result);
        if (expected == VehicleCommandResult.Accepted)
        {
            encoder.Received(1).EncodeCommandLong(1, 1, MavLinkCommandIds.PreflightRebootShutdown,
                Arg.Is<IReadOnlyList<float>>(p => p.Count == 7 && p[0] == 5 && p.Skip(1).All(value => value == 0)));
        }
        else
        {
            Assert.DoesNotContain(protocol.ReceivedCalls(), call => call.GetMethodInfo().Name == "SendRawAsync");
        }
    }
}
