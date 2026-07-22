using FluentAssertions;
using MissionPlanner.App.Configuration;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
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
    public void ApplicationStateTracksAndRetainsDerivedVehicleName()
    {
        var session = CreateSession(1, 2);
        var activeVehicle = Substitute.For<IActiveVehicleContext>();
        activeVehicle.Current.Returns(ActiveVehicleSnapshot.Empty);
        using var state = new ApplicationStateService(activeVehicle);

        var connected = new ActiveVehicleSnapshot(session.Id, session.State);
        activeVehicle.Current.Returns(connected);
        activeVehicle.Changed += Raise.Event<EventHandler<ActiveVehicleChangedEventArgs>>(
            activeVehicle,
            new ActiveVehicleChangedEventArgs(ActiveVehicleSnapshot.Empty, connected));
        state.VehicleId.Should().Be(session.Id);
        state.VehicleName.Should().Be("SysID 1:Copter");

        session.ApplyHeartbeat(0, 1, 3, 0, 4, 3, DateTimeOffset.UtcNow);
        var updated = new ActiveVehicleSnapshot(session.Id, session.State);
        activeVehicle.Current.Returns(updated);
        activeVehicle.Changed += Raise.Event<EventHandler<ActiveVehicleChangedEventArgs>>(
            activeVehicle,
            new ActiveVehicleChangedEventArgs(connected, updated));
        state.VehicleName.Should().Be("SysID 1:Plane");

        var disconnectedState = session.State with
        {
            Connection = session.State.Connection with { State = VehicleConnectionState.Offline }
        };
        var disconnected = new ActiveVehicleSnapshot(session.Id, disconnectedState);
        activeVehicle.Current.Returns(disconnected);
        activeVehicle.Changed += Raise.Event<EventHandler<ActiveVehicleChangedEventArgs>>(
            activeVehicle,
            new ActiveVehicleChangedEventArgs(updated, disconnected));
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
