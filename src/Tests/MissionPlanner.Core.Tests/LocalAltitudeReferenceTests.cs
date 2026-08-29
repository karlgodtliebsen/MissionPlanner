using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Validates per-session, relative-only local altitude display zeroes.</summary>
public sealed class LocalAltitudeReferenceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void ZeroAndResetTransformOnlyRelativeAltitudeForTargetVehicle()
    {
        var eventHub = EventHub();
        var references = new LocalAltitudeReferenceService(eventHub);
        var vehicleA = new VehicleId(1, 1);
        var vehicleB = new VehicleId(2, 1);
        var sessionA = Session(vehicleA, 37.5, 137.5);
        var sessionB = Session(vehicleB, 12, 112);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(vehicleA).Returns(sessionA);
        registry.GetRequired(vehicleB).Returns(sessionB);
        var hud = new VehicleHudDataService(registry, eventHub, Substitute.For<ILogger<VehicleHudDataService>>(), references);

        references.TryZero(vehicleA, 37.5).Should().BeTrue();
        hud.GetHudData(vehicleA)!.Altitude.Should().Be(0);
        hud.GetHudData(vehicleB)!.Altitude.Should().Be(12);

        sessionA.ApplyGlobalPosition(new MissionPlanner.Core.Vehicles.Observations.VehicleGlobalPositionObservation(0, 0, 142, 42, null, null, null, null, Now));
        hud.Dispose();
        hud = new VehicleHudDataService(registry, eventHub, Substitute.For<ILogger<VehicleHudDataService>>(), references);
        hud.GetHudData(vehicleA)!.Altitude.Should().Be(4.5);

        references.Reset(vehicleA);
        hud.GetHudData(vehicleA)!.Altitude.Should().Be(42);
        hud.Dispose();
    }

    [Fact]
    public void MissingRelativeAltitudeUsesUnmodifiedMslFallback()
    {
        var eventHub = EventHub();
        var references = new LocalAltitudeReferenceService(eventHub);
        var vehicle = new VehicleId(1, 1);
        var session = Session(vehicle, null, 142);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(vehicle).Returns(session);
        references.TryZero(vehicle, 37.5).Should().BeTrue();
        using var hud = new VehicleHudDataService(registry, eventHub, Substitute.For<ILogger<VehicleHudDataService>>(), references);

        hud.GetHudData(vehicle)!.Altitude.Should().Be(142);
        references.TryZero(vehicle, double.NaN).Should().BeFalse();
        references.TryZero(vehicle, double.PositiveInfinity).Should().BeFalse();
    }

    [Fact]
    public async Task DisconnectAndReplacementClearReference()
    {
        Func<VehicleDisconnected, CancellationToken, Task>? disconnected = null;
        Func<VehicleRegistered, CancellationToken, Task>? registered = null;
        var eventHub = Substitute.For<IDomainEventHub>();
        eventHub.SubscribeDomainEventAsync(Arg.Any<Func<VehicleDisconnected, CancellationToken, Task>>()).Returns(call =>
        {
            disconnected = call.Arg<Func<VehicleDisconnected, CancellationToken, Task>>();
            return Substitute.For<IDisposable>();
        });
        eventHub.SubscribeDomainEventAsync(Arg.Any<Func<VehicleRegistered, CancellationToken, Task>>()).Returns(call =>
        {
            registered = call.Arg<Func<VehicleRegistered, CancellationToken, Task>>();
            return Substitute.For<IDisposable>();
        });
        var references = new LocalAltitudeReferenceService(eventHub);
        var vehicle = new VehicleId(1, 1);

        references.TryZero(vehicle, 10).Should().BeTrue();
        await disconnected!(new VehicleDisconnected(vehicle, Now), TestContext.Current.CancellationToken);
        references.HasReference(vehicle).Should().BeFalse();
        references.TryZero(vehicle, 11).Should().BeTrue();
        await registered!(new VehicleRegistered(vehicle), TestContext.Current.CancellationToken);
        references.HasReference(vehicle).Should().BeFalse();
    }

    private static IDomainEventHub EventHub()
    {
        var hub = Substitute.For<IDomainEventHub>();
        hub.SubscribeDomainEventAsync(Arg.Any<Func<VehicleStateUpdated, CancellationToken, Task>>()).Returns(Substitute.For<IDisposable>());
        hub.SubscribeDomainEventAsync(Arg.Any<Func<VehicleDisconnected, CancellationToken, Task>>()).Returns(Substitute.For<IDisposable>());
        hub.SubscribeDomainEventAsync(Arg.Any<Func<VehicleRegistered, CancellationToken, Task>>()).Returns(Substitute.For<IDisposable>());
        return hub;
    }

    private static VehicleSession Session(VehicleId id, double? relative, double? msl)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var state = new VehicleState(id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, Now, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null) with
        {
            Position = VehiclePositionState.Empty with { RelativeAltitudeMeters = relative, AltitudeMslMeters = msl, ObservedAt = Now }
        };
        return new VehicleSession(state, new TransportEndPoint("test"), clock);
    }
}
