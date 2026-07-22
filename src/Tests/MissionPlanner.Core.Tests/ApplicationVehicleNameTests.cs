using FluentAssertions;
using MissionPlanner.App.Configuration;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Verifies synchronization of derived vehicle names into application presentation state.
/// </summary>
public sealed class ApplicationVehicleNameTests
{
    /// <summary>
    /// Verifies connect, identity correction, and disconnect presentation behavior.
    /// </summary>
    [Fact]
    public async Task ApplicationStateTracksAndRetainsDerivedVehicleName()
    {
        Func<VehicleConnected, CancellationToken, Task>? connected = null;
        Func<VehicleDisconnected, CancellationToken, Task>? disconnected = null;
        Func<VehicleStateUpdated, CancellationToken, Task>? updated = null;
        var eventHub = Substitute.For<IDomainEventHub>();
        eventHub.SubscribeDomainEventAsync(Arg.Do<Func<VehicleConnected, CancellationToken, Task>>(handler => connected = handler))
            .Returns(Substitute.For<IDisposable>());
        eventHub.SubscribeDomainEventAsync(Arg.Do<Func<VehicleDisconnected, CancellationToken, Task>>(handler => disconnected = handler))
            .Returns(Substitute.For<IDisposable>());
        eventHub.SubscribeDomainEventAsync(Arg.Do<Func<VehicleStateUpdated, CancellationToken, Task>>(handler => updated = handler))
            .Returns(Substitute.For<IDisposable>());

        var session = CreateSession(1, 2);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(session.Id).Returns(session);
        using var state = new ApplicationStateService(eventHub, registry);

        await connected!(new VehicleConnected(session.Id, "UDP", "14550", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        state.VehicleId.Should().Be(session.Id);
        state.VehicleName.Should().Be("SysID 1:Copter");

        session.ApplyHeartbeat(0, 1, 3, 0, 4, 3, DateTimeOffset.UtcNow);
        await updated!(new VehicleStateUpdated(session.State), TestContext.Current.CancellationToken);
        state.VehicleName.Should().Be("SysID 1:Plane");

        await disconnected!(new VehicleDisconnected(session.Id, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        state.IsConnected.Should().BeFalse();
        state.VehicleName.Should().Be("SysID 1:Plane");
    }

    private static VehicleSession CreateSession(byte systemId, byte mavType)
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(new VehicleId(systemId, 1), 0, mavType, 3, 0, 4, 3, VehicleConnectionState.Online, now, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return new VehicleSession(state, new TransportEndPoint("test"), clock);
    }
}
