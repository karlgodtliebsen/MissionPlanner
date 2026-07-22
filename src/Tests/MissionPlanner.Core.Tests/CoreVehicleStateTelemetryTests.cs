using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Core.Vehicles.Observations;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Validates promotion of core flight-display telemetry into vehicle state.</summary>
public sealed class CoreVehicleStateTelemetryTests
{
    private static readonly DateTimeOffset ObservedAt = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly TransportEndPoint EndPoint = new("test", "127.0.0.1:14550");

    /// <summary>Verifies quaternion and extended-system messages update normalized flight state.</summary>
    [Fact]
    public async Task FlightHandlerAppliesQuaternionAndLandedState()
    {
        var (session, registry, eventHub) = CreateSession();
        var handler = new FlightTelemetryHandler(registry, eventHub);
        var halfAngle = Math.PI / 4;

        await handler.HandleAsync(new AttitudeQuaternionMessage(1, 1, EndPoint, 10, (float)Math.Cos(halfAngle), 0, 0, (float)Math.Sin(halfAngle), 0.1f, 0.2f, 0.3f, new float[4], ObservedAt), TestContext.Current.CancellationToken);
        await handler.HandleAsync(new ExtendedSysStateMessage(1, 1, EndPoint, 4, 2, ObservedAt), TestContext.Current.CancellationToken);

        Assert.Equal(Math.PI / 2, session.State.Motion.YawRadians!.Value, 5);
        Assert.Equal(0.3, session.State.Motion.YawRateRadiansPerSecond!.Value, 5);
        Assert.Equal(VehicleVtolState.FixedWing, session.State.Flight.VtolState);
        Assert.Equal(VehicleLandedState.InAir, session.State.Flight.LandedState);
        Assert.False(session.State.Flight.IsStale(ObservedAt.AddSeconds(1), TimeSpan.FromSeconds(2)));
    }

    /// <summary>Verifies GPS2 sentinels and scaling are isolated from the primary receiver.</summary>
    [Fact]
    public async Task NavigationHandlerAppliesSecondaryGpsWithNormalizedUnits()
    {
        var (session, registry, eventHub) = CreateSession();
        var handler = new NavigationTelemetryHandler(registry, eventHub);
        var message = new Gps2RawMessage(1, 1, EndPoint, 1, 6, 550_000_000, 120_000_000, 12_000, 125, ushort.MaxValue, 1234, 9000, byte.MaxValue, 0, 0, 0, 0, 1500, uint.MaxValue, 0, 0, ObservedAt);

        await handler.HandleAsync(message, TestContext.Current.CancellationToken);

        var gps2 = Assert.IsType<VehicleGpsReceiverState>(session.State.Gps.SecondaryReceiver);
        Assert.Equal(GpsFixType.RtkFloat, gps2.FixType);
        Assert.Null(gps2.SatellitesVisible);
        Assert.Equal(1.25, gps2.HorizontalDilution);
        Assert.Null(gps2.VerticalDilution);
        Assert.Equal(12.34, gps2.GroundSpeedMetersPerSecond);
        Assert.Equal(1.5, gps2.HorizontalAccuracyMeters);
        Assert.Null(gps2.VerticalAccuracyMeters);
    }

    /// <summary>Verifies secondary battery, radio-link, and servo telemetry update their cohesive slices.</summary>
    [Fact]
    public async Task PowerAndRadioHandlersApplySecondaryTelemetry()
    {
        var (session, registry, eventHub) = CreateSession();
        var power = new PowerTelemetryHandler(registry, eventHub);
        var radio = new RadioTelemetryHandler(registry, eventHub);

        await power.HandleAsync(new SysStatusMessage(1, 1, EndPoint, 80, 11.5f, ObservedAt, 3.2, 0x7, 0x3, 0x1, 42.5, 0.25, 6), TestContext.Current.CancellationToken);
        await power.HandleAsync(new Battery2Message(1, 1, EndPoint, 12_300, 456, ObservedAt), TestContext.Current.CancellationToken);
        await radio.HandleAsync(new RadioStatusMessage(1, 1, EndPoint, 100, 90, 80, 20, 30, 4, 5, ObservedAt), TestContext.Current.CancellationToken);
        await radio.HandleAsync(new ServoOutputRawMessage(1, 1, EndPoint, 1, 2, [1000, 1500, 2000], ObservedAt), TestContext.Current.CancellationToken);

        var battery2 = Assert.IsType<VehicleBatteryState>(session.State.Power.SecondaryBattery);
        Assert.Equal(12.3, battery2.VoltageVolts);
        Assert.Equal(4.56, battery2.CurrentAmps);
        Assert.Equal(3.2, session.State.Power.BatteryCurrentAmps);
        Assert.Equal((uint)0x7, session.State.Health.SensorsPresent);
        Assert.Equal((uint)0x1, session.State.Health.SensorsHealthy);
        Assert.Equal(42.5, session.State.Health.ControllerLoadPercent);
        Assert.Equal(100, session.State.Radio.LocalRssi);
        Assert.Equal(90, session.State.Radio.RemoteRssi);
        Assert.Equal((ushort)4, session.State.Radio.ReceiveErrors);
        Assert.Equal((byte)2, session.State.Radio.ServoOutputPort);
        Assert.Equal([1000, 1500, 2000], session.State.Radio.ServoOutputsRaw);
    }

    /// <summary>Verifies duplicate telemetry does not publish a redundant vehicle-state event.</summary>
    [Fact]
    public async Task DuplicateTelemetryPublishesOnlyOneStateChange()
    {
        var (_, registry, eventHub) = CreateSession();
        var handler = new RadioTelemetryHandler(registry, eventHub);
        var message = new RadioMessage(1, 1, EndPoint, 100, 90, 80, 20, 30, 4, 5, ObservedAt);

        await handler.HandleAsync(message, TestContext.Current.CancellationToken);
        await handler.HandleAsync(message, TestContext.Current.CancellationToken);

        await eventHub.Received(1).PublishDomainEventAsync(Arg.Any<VehicleStateUpdated>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies the message pump dispatches promoted telemetry through the normal event topic.</summary>
    [Fact]
    public async Task MessagePumpDispatchesPromotedTelemetry()
    {
        var (session, registry, domainEventHub) = CreateSession();
        var handler = new RadioTelemetryHandler(registry, domainEventHub);
        IVehicleMessageHandler[] handlers = [handler];
        var dispatcher = new VehicleMessageDispatcher(handlers);
        var eventHub = Substitute.For<IEventHub>();
        Func<MavLinkMessage, CancellationToken, Task>? callback = null;
        eventHub.SubscribeAsync(MavLinkEventTopics.ReceivedMessage, Arg.Any<Func<MavLinkMessage, CancellationToken, Task>>())
            .Returns(call =>
            {
                callback = call.Arg<Func<MavLinkMessage, CancellationToken, Task>>();
                return Substitute.For<IDisposable>();
            });
        await using var pump = new VehicleMessagePump(dispatcher, eventHub, NullLogger<VehicleMessagePump>.Instance);
        await pump.StartAsync(TestContext.Current.CancellationToken);

        await callback!(new ServoOutputRawMessage(1, 1, EndPoint, 1, 0, [1100, 1200], ObservedAt), TestContext.Current.CancellationToken);

        Assert.Equal([1100, 1200], session.State.Radio.ServoOutputsRaw);
    }

    /// <summary>Verifies direct session application preserves primary state and exposes stale semantics.</summary>
    [Fact]
    public void SessionAppliesFocusedObservations()
    {
        var (session, _, _) = CreateSession();
        session.ApplyGps(new VehicleGpsObservation(GpsFixType.Fix3D, 12, 0.8, 1.1, 5, 90, 0.5, 0.8, ObservedAt));
        session.ApplyBattery(new VehicleBatteryObservation(11.1, 2.5, 50, 0.55, 75, ObservedAt));

        Assert.Equal(12, session.State.Gps.SatellitesVisible);
        Assert.Equal(75, session.State.Power.BatteryRemainingPercent);
        Assert.True(session.State.Gps.IsStale(ObservedAt.AddSeconds(4), TimeSpan.FromSeconds(3)));
        Assert.True(session.State.Power.IsStale(ObservedAt.AddSeconds(6), TimeSpan.FromSeconds(5)));
    }

    private static (VehicleSession Session, IVehicleRegistry Registry, IDomainEventHub EventHub) CreateSession()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(ObservedAt);
        var state = new VehicleState(new VehicleId(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, ObservedAt, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
        var session = new VehicleSession(state, EndPoint, dateTimeProvider);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(session.Id).Returns(session);
        registry.Vehicles.Returns([session]);
        var eventHub = Substitute.For<IDomainEventHub>();
        return (session, registry, eventHub);
    }
}
