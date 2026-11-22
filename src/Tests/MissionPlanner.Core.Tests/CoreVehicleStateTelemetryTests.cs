using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Core.Vehicles.Observations;
using MissionPlanner.Firmware;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Shared.Models.Vehicles.Models;
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

    /// <summary>Heartbeats preserve landed and VTOL evidence used by the disarm safety policy.</summary>
    [Theory]
    [InlineData(VehicleLandedState.OnGround, 1, false)]
    [InlineData(VehicleLandedState.InAir, 1, true)]
    [InlineData(VehicleLandedState.OnGround, 10, true)]
    public void HeartbeatPreservesExtendedFlightState(VehicleLandedState landedState, int ageSeconds, bool requiresConfirmation)
    {
        var (session, _, _) = CreateSession();
        session.ApplyExtendedFlightState(new VehicleExtendedFlightStateObservation(
            VehicleVtolState.FixedWing, landedState, ObservedAt));
        var heartbeatAt = ObservedAt.AddSeconds(ageSeconds);

        session.ApplyHeartbeat(new VehicleHeartbeatObservation(4, 2, 3, 129, 5, 3, heartbeatAt));

        var state = session.State;
        Assert.Equal((uint)4, state.Flight.CustomMode);
        Assert.Equal((byte)129, state.Flight.BaseMode);
        Assert.Equal((byte)5, state.Flight.SystemStatus);
        Assert.Equal(VehicleMode.Guided, state.Flight.Mode);
        Assert.True(state.IsArmed);
        Assert.Equal(heartbeatAt, state.LastHeartbeatAt);
        Assert.Equal(VehicleVtolState.FixedWing, state.Flight.VtolState);
        Assert.Equal(landedState, state.Flight.LandedState);
        Assert.Equal(ObservedAt, state.Flight.ObservedAt);

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(heartbeatAt);
        var decision = new VehicleCommandPolicy(clock).Evaluate(state, VehicleAction.Disarm);
        Assert.True(decision.IsAllowed, decision.Reason);
        Assert.Equal(requiresConfirmation, decision.RequiresConfirmation);
    }

    /// <summary>A newer heartbeat cannot make old extended flight telemetry fresh enough to arm.</summary>
    [Fact]
    public void HeartbeatDoesNotRefreshExtendedFlightState()
    {
        var (session, _, _) = CreateSession();
        session.ApplyExtendedFlightState(new VehicleExtendedFlightStateObservation(
            VehicleVtolState.FixedWing, VehicleLandedState.OnGround, ObservedAt));
        var heartbeatAt = ObservedAt.AddSeconds(10);

        session.ApplyHeartbeat(new VehicleHeartbeatObservation(0, 2, 3, 0, 4, 3, heartbeatAt));

        Assert.Equal(heartbeatAt, session.State.LastHeartbeatAt);
        Assert.Equal(ObservedAt, session.State.Flight.ObservedAt);
        Assert.True(session.State.Flight.IsStale(heartbeatAt, TimeSpan.FromSeconds(5)));
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(heartbeatAt);
        var decision = new VehicleCommandPolicy(clock).Evaluate(session.State, VehicleAction.Arm);
        Assert.False(decision.IsAllowed);
        Assert.Equal("Fresh flight-state telemetry is required before arming.", decision.Reason);
    }

    /// <summary>A heartbeat alone cannot establish that an armed vehicle is on the ground.</summary>
    [Fact]
    public void HeartbeatWithoutExtendedFlightStateRequiresDisarmConfirmation()
    {
        var (session, _, _) = CreateSession();
        session.ApplyHeartbeat(new VehicleHeartbeatObservation(0, 2, 3, 128, 4, 3, ObservedAt));

        Assert.Equal(VehicleVtolState.Undefined, session.State.Flight.VtolState);
        Assert.Equal(VehicleLandedState.Undefined, session.State.Flight.LandedState);
        Assert.Null(session.State.Flight.ObservedAt);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ObservedAt);
        var decision = new VehicleCommandPolicy(clock).Evaluate(session.State, VehicleAction.Disarm);
        Assert.True(decision.IsAllowed);
        Assert.True(decision.RequiresConfirmation);
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
        Assert.Equal(GpsFixType.RtkFixed, gps2.FixType); // MAVLink fix type 6 is RTK fixed.
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

    /// <summary>Rail updates and ambiguous summaries cannot freshen or overwrite per-instance battery evidence.</summary>
    [Fact]
    public void BatterySamplesKeepTheirOwnIdentityValidityAndAge()
    {
        var (session, _, _) = CreateSession();
        session.ApplyBattery(new VehicleBatteryObservation(12, 1, null, null, 99, ObservedAt));
        session.ApplyBattery(new VehicleBatteryObservation(24, 2, null, null, 80, ObservedAt, 1));
        session.ApplyPowerRail(new VehiclePowerRailObservation(5, 5, 0, ObservedAt.AddSeconds(20)));
        Assert.Equal(ObservedAt, session.State.Power.Batteries[0].ObservedAt);
        Assert.Equal(24, session.State.Power.Batteries[1].VoltageVolts);
        session.ApplyBattery(new VehicleBatteryObservation(99, null, null, null, 100, ObservedAt.AddSeconds(21)) { IsInstanceSpecific = false });
        Assert.Equal(12, session.State.Power.Batteries[0].VoltageVolts);
        session.ApplyBattery(new VehicleBatteryObservation(null, null, null, null, null, ObservedAt.AddSeconds(22)));
        Assert.Null(session.State.Power.BatteryVoltageVolts);
        Assert.Null(session.State.Power.Batteries[0].VoltageVolts);
        Assert.Null(session.State.Power.BatteryRemainingPercent);
        Assert.Equal(24, session.State.Power.Batteries[1].VoltageVolts);
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
