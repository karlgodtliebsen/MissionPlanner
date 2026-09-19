using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.MavFtp;
using MissionPlanner.MavLink.MavFtp.Abstractions;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class ConnectionOperationCancellationTests
{
    [Fact]
    public async Task ConnectionLossCompletesCommandAckWaiterWithoutTimeout()
    {
        var activity = new MavLinkConnectionActivity(TimeProvider.System);
        var protocol = Substitute.For<IMavLinkConnection>();
        protocol.Activity.Returns(activity);
        var session = Substitute.For<IVehicleConnectionSession>();
        session.Connection.Returns(protocol);
        var registry = Substitute.For<IVehicleRegistry>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        var state = new VehicleState(new VehicleId(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, clock.UtcNow,
            VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
        registry.GetRequired(new(1, 1)).Returns(new VehicleSession(state, new TransportEndPoint("test"), clock));
        var policy = Substitute.For<IVehicleCommandPolicy>();
        policy.Evaluate(Arg.Any<VehicleState>(), Arg.Any<VehicleAction>()).Returns(new VehicleCommandDecision(true, false, null));
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        protocol.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>())
            .Returns(_ => { sent.TrySetResult(); return ValueTask.CompletedTask; });
        var service = new VehicleCommandService(registry, Substitute.For<IDomainEventHub>(), session,
            Substitute.For<IMavLinkCommandEncoder>(), new CommandAckTracker(), clock, policy, Substitute.For<IArduPilotModeCatalog>());
        var command = service.ArmAsync(new(1, 1), TestContext.Current.CancellationToken);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        activity.End("TransportFault");
        var result = await command.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.Equal(VehicleCommandResult.ConnectionLost, result.Result);
    }

    [Fact]
    public async Task ParameterStreamStopsOnSharedConnectionLifetime()
    {
        var activity = new MavLinkConnectionActivity(TimeProvider.System);
        var parameters = Substitute.For<IVehicleParameterService>();
        parameters.ConnectionCancellationToken.Returns(activity.LifetimeToken);
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        parameters.RequestParameterListAsync(Arg.Any<VehicleId>(), Arg.Any<CancellationToken>())
            .Returns(_ => { requested.TrySetResult(); return true; });
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        var service = new VehicleParameterStreamService(parameters, Substitute.For<IVehicleParameterRegistry>(),
            Substitute.For<IDomainEventHub>(), clock, NullLogger<VehicleParameterStreamService>.Instance,
            Substitute.For<IArduPilotPackedParameterDecoder>());
        var read = service.StreamAllParametersWithRetryAsync(new(1, 1), maxRetries: 0, cancellationToken: TestContext.Current.CancellationToken);
        await requested.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        activity.End("CommunicationTimeout");
        var result = await read.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Equal("Connection lost", result.ErrorMessage);
    }

    [Fact]
    public async Task MavFtpRequestCancelsAndReleasesTargetRegistration()
    {
        var activity = new MavLinkConnectionActivity(TimeProvider.System);
        var protocol = Substitute.For<IMavLinkConnection>();
        protocol.Activity.Returns(activity);
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        protocol.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>())
            .Returns(_ => { sent.TrySetResult(); return ValueTask.CompletedTask; });
        var codec = new MavFtpPacketCodec();
        using var dispatcher = new MavFtpResponseDispatcher(Substitute.For<IEventHub>(), codec,
            Options.Create(new MavFtpOptions()), NullLogger<MavFtpResponseDispatcher>.Instance);
        var sequence = new MavFtpSequenceStore();
        await using var service = new MavFtpClient(protocol, new MavFtpMessageEncoder(new CommonMavLinkCrcExtraProvider()), codec, dispatcher,
            sequence, Options.Create(new MavFtpOptions()), NullLogger<MavFtpClient>.Instance);
        var target = new MavFtpTarget(1, 1, new TransportEndPoint("udp"));
        var read = service.GetFileInfoAsync(target, "/test", TestContext.Current.CancellationToken);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        activity.End("TransportFault");
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            read.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
        Assert.Contains("Connection lost", error.Message);
        using var lease = await sequence.EnterOperationAsync(target, TestContext.Current.CancellationToken);
    }
}
