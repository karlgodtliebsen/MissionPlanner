using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Test.Support;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class VehicleConnectionHealthTests
{
    [Fact]
    public async Task AnyValidVehicleTrafficKeepsOnlineAndRecoveryKeepsLifetime()
    {
        await using var fixture = new Fixture();
        var originalHeartbeat = fixture.Activity.LastHeartbeatAt(1);
        fixture.Clock.Advance(TimeSpan.FromSeconds(3));
        await fixture.Tick();
        Assert.Equal(VehicleConnectionState.Degraded, fixture.Vehicle.State.ConnectionState);
        Assert.False(fixture.Activity.LifetimeToken.IsCancellationRequested);
        Assert.Equal(0, fixture.Disconnections);
        fixture.Activity.ReportValidFrame(1, false, fixture.Clock.GetUtcNow());
        await fixture.Tick();
        Assert.Equal(VehicleConnectionState.Online, fixture.Vehicle.State.ConnectionState);
        Assert.Equal(originalHeartbeat, fixture.Activity.LastHeartbeatAt(1));
        Assert.Equal(fixture.Clock.GetUtcNow(), fixture.Vehicle.State.Connection.LastPacketAt);
        for (var index = 0; index < 6; index++)
        {
            fixture.Clock.Advance(TimeSpan.FromSeconds(2));
            fixture.Activity.ReportValidFrame(1, false, fixture.Clock.GetUtcNow());
            await fixture.Tick();
        }
        Assert.Equal(0, fixture.Disconnections);
        Assert.False(fixture.Activity.LifetimeToken.IsCancellationRequested);
    }

    [Theory]
    [InlineData("serial")]
    [InlineData("tcp")]
    [InlineData("udp")]
    public async Task HardTimeoutDisconnectsOpenTransportExactlyOnce(string transport)
    {
        await using var fixture = new Fixture(transport: transport);
        fixture.Clock.Advance(TimeSpan.FromSeconds(11));
        // Traffic for another system must not revive this vehicle.
        fixture.Activity.ReportValidFrame(2, false, fixture.Clock.GetUtcNow());
        await fixture.Tick();
        await fixture.Tick();
        Assert.Equal(1, fixture.Disconnections);
        Assert.Equal("CommunicationTimeout", fixture.Reason);
        Assert.Equal(VehicleConnectionState.Offline, fixture.Vehicle.State.ConnectionState);
        Assert.True(fixture.Activity.LifetimeToken.IsCancellationRequested);
        Assert.Equal(12.3, fixture.Vehicle.State.Position.LatitudeDegrees);
    }

    [Fact]
    public async Task TransportFaultImmediatelyDisconnectsDuringStartupGraceAndDuplicateReportsDoNothing()
    {
        await using var fixture = new Fixture(armed: true);
        fixture.Activity.End("TransportFault");
        fixture.Activity.End("TransportClosed");
        await fixture.Tick();
        Assert.Equal(1, fixture.Disconnections);
        Assert.Equal("TransportFault", fixture.Reason);
        await fixture.Notifications.DidNotReceive().NotifyAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArmedWarningsAreThrottledAndRecoveryClearsWarning()
    {
        await using var fixture = new Fixture(armed: true, grace: TimeSpan.Zero);
        fixture.Clock.Advance(TimeSpan.FromSeconds(3));
        await fixture.Tick();
        Assert.Contains($"{3:F1}s", fixture.Vehicle.State.Connection.Warning);
        await fixture.Notifications.Received(1).NotifyAsync(
            Arg.Is<UserNotification>(notice => notice != null && notice.Severity == UserNotificationSeverity.Error), Arg.Any<CancellationToken>());
        fixture.Clock.Advance(TimeSpan.FromSeconds(4));
        await fixture.Tick();
        await fixture.Notifications.Received(1).NotifyAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        await fixture.Tick();
        await fixture.Notifications.Received(2).NotifyAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());
        fixture.Activity.ReportValidFrame(1, false, fixture.Clock.GetUtcNow());
        await fixture.Tick();
        Assert.Null(fixture.Vehicle.State.Connection.Warning);
        Assert.Equal(0, fixture.Disconnections);
    }

    [Fact]
    public async Task ExplicitStopDoesNotProduceUnexpectedLoss()
    {
        await using var fixture = new Fixture(armed: true, grace: TimeSpan.Zero);
        fixture.Lease.Dispose();
        fixture.Activity.End("UserRequested");
        fixture.Clock.Advance(TimeSpan.FromSeconds(30));
        await fixture.Tick();
        Assert.Equal(0, fixture.Disconnections);
        await fixture.Notifications.DidNotReceive().NotifyAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActiveContextKeepsLifetimeThroughDegradationAndRejectsLateOnlineEvents()
    {
        var events = Substitute.For<IDomainEventHub>();
        Func<VehicleConnected, CancellationToken, Task> connected = null!;
        Func<VehicleDisconnected, CancellationToken, Task> disconnected = null!;
        Func<VehicleStateUpdated, CancellationToken, Task> updated = null!;
        events.SubscribeDomainEventAsync(Arg.Do<Func<VehicleConnected, CancellationToken, Task>>(handler => connected = handler))
            .Returns(Substitute.For<IDisposable>());
        events.SubscribeDomainEventAsync(Arg.Do<Func<VehicleDisconnected, CancellationToken, Task>>(handler => disconnected = handler))
            .Returns(Substitute.For<IDisposable>());
        events.SubscribeDomainEventAsync(Arg.Do<Func<VehicleStateUpdated, CancellationToken, Task>>(handler => updated = handler))
            .Returns(Substitute.For<IDisposable>());
        var now = DateTimeOffset.UtcNow;
        var id = new VehicleId(1, 1);
        var state = new VehicleState(id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
            VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(id).Returns(new VehicleSession(state, new TransportEndPoint("test"), Substitute.For<IDateTimeProvider>()));
        using var context = new ActiveVehicleContext(events, registry);
        await connected(new VehicleConnected(id, "test", "test", now), TestContext.Current.CancellationToken);
        var lifetime = context.ConnectionCancellationToken;
        await updated(new VehicleStateUpdated(state with { Connection = state.Connection with { State = VehicleConnectionState.Degraded } }),
            TestContext.Current.CancellationToken);
        Assert.False(lifetime.IsCancellationRequested);
        await updated(new VehicleStateUpdated(state), TestContext.Current.CancellationToken);
        Assert.Equal(lifetime, context.ConnectionCancellationToken);
        await disconnected(new VehicleDisconnected(id, now, "TransportFault"), TestContext.Current.CancellationToken);
        Assert.True(lifetime.IsCancellationRequested);
        await updated(new VehicleStateUpdated(state), TestContext.Current.CancellationToken);
        Assert.False(context.IsOnline);
        await connected(new VehicleConnected(id, "test", "test", now), TestContext.Current.CancellationToken);
        Assert.True(context.IsOnline);
        Assert.NotEqual(lifetime, context.ConnectionCancellationToken);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ManualTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);
        public MavLinkConnectionActivity Activity { get; }
        public VehicleSession Vehicle { get; }
        public IUserNotificationService Notifications { get; } = Substitute.For<IUserNotificationService>();
        public VehicleConnectionMonitor Monitor { get; }
        public IDisposable Lease { get; }
        public int Disconnections;
        public string? Reason;
        public Fixture(bool armed = false, TimeSpan? grace = null, string transport = "udp")
        {
            Activity = new(Clock);
            Activity.ReportValidFrame(1, true, Clock.GetUtcNow());
            var state = new VehicleState(new VehicleId(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, Clock.GetUtcNow(),
                VehicleMode.Unknown, armed, 12.3, null, null, null, null, null, null, null);
            Vehicle = new(state, new TransportEndPoint(transport), Substitute.For<IDateTimeProvider>());
            var registry = Substitute.For<IVehicleRegistry>();
            registry.GetRequired(new(1, 1)).Returns(Vehicle);
            var session = Substitute.For<IVehicleConnectionSession>();
            var protocol = Substitute.For<IMavLinkConnection>();
            protocol.Activity.Returns(Activity);
            session.Connection.Returns(protocol);
            session.ActiveTransportProtocol.Returns(transport);
            Monitor = new(registry, Substitute.For<IDomainEventHub>(), Clock,
                Options.Create(new VehicleConnectionHealthOptions { NotificationGracePeriod = grace ?? TimeSpan.FromSeconds(30) }),
                NullLogger<VehicleConnectionMonitor>.Instance, Notifications);
            Lease = Monitor.Track(new(1, 1), Guid.NewGuid(), session, reason =>
            {
                Reason = reason;
                Interlocked.Increment(ref Disconnections);
                return Task.CompletedTask;
            })!;
        }
        public Task Tick() => Monitor.UpdateConnectionStatesAsync(TestContext.Current.CancellationToken);
        public async ValueTask DisposeAsync()
        {
            Lease.Dispose();
            await Monitor.DisposeAsync();
        }
    }
}
