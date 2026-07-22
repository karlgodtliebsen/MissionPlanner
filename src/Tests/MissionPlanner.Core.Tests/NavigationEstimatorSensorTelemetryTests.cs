using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Validates promoted estimator, sensor, environment, and time telemetry.</summary>
public sealed class NavigationEstimatorSensorTelemetryTests
{
    private static readonly DateTimeOffset ObservedAt = new(2026, 7, 22, 13, 0, 0, TimeSpan.Zero);
    private static readonly TransportEndPoint EndPoint = new("test");

    /// <summary>Verifies pressure instances and range sentinels are normalized independently.</summary>
    [Fact]
    public async Task SensorHandlerAppliesMultiplePressureAndRangeInstances()
    {
        var (session, registry, eventHub) = CreateSession();
        var handler = new SensorTelemetryHandler(registry, eventHub);
        await handler.HandleAsync(new ScaledPressureMessage(1, 1, EndPoint, 1, 1013.25f, 2.5f, 2450, 1800, ObservedAt), TestContext.Current.CancellationToken);
        await handler.HandleAsync(new ScaledPressure2Message(1, 1, EndPoint, 1, 900f, 1.5f, 2000, 1500, ObservedAt), TestContext.Current.CancellationToken);
        await handler.HandleAsync(new DistanceSensorMessage(1, 1, EndPoint, 1, 20, 800, ushort.MaxValue, 0, 7, 25, 10, 0, 0, new float[4], byte.MaxValue, ObservedAt), TestContext.Current.CancellationToken);

        Assert.Equal(1013.25, session.State.Pressure.Primary!.AbsoluteHectopascals);
        Assert.Equal(24.5, session.State.Pressure.Primary.TemperatureCelsius);
        Assert.Equal(1, session.State.Pressure.Secondary!.Instance);
        var range = session.State.Range.Sensors[7];
        Assert.Null(range.DistanceMeters);
        Assert.Equal(0.2, range.MinimumMeters);
        Assert.Null(range.SignalQualityPercent);
    }

    /// <summary>Verifies wind, altitude, terrain, and vehicle-clock conversions.</summary>
    [Fact]
    public async Task SensorHandlerAppliesEnvironmentAndTimeUnits()
    {
        var (session, registry, eventHub) = CreateSession();
        var handler = new SensorTelemetryHandler(registry, eventHub);
        await handler.HandleAsync(new WindMessage(1, 1, EndPoint, 90, 10, -2, ObservedAt), TestContext.Current.CancellationToken);
        await handler.HandleAsync(new TerrainReportMessage(1, 1, EndPoint, 0, 0, 100, 120, 30, 0, 1, ObservedAt), TestContext.Current.CancellationToken);
        await handler.HandleAsync(new AltitudeMessage(1, 1, EndPoint, 1, float.NaN, 150, 20, 30, 120, 5, ObservedAt), TestContext.Current.CancellationToken);
        await handler.HandleAsync(new SystemTimeMessage(1, 1, EndPoint, 1_000_000, 2500, ObservedAt), TestContext.Current.CancellationToken);

        Assert.Equal(0, session.State.Environment.WindNorthMetersPerSecond!.Value, 5);
        Assert.Equal(10, session.State.Environment.WindEastMetersPerSecond!.Value, 5);
        Assert.Null(session.State.Environment.AltitudeMonotonicMeters);
        Assert.Equal(30, session.State.Environment.AltitudeRelativeMeters);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(1), session.State.Time.UnixTime);
        Assert.Equal(TimeSpan.FromSeconds(2.5), session.State.Time.BootTime);
        Assert.True(session.State.Environment.IsStale(ObservedAt.AddSeconds(4), TimeSpan.FromSeconds(3)));
    }

    /// <summary>Verifies AHRS instances and vibration diagnostics update coherent state slices.</summary>
    [Fact]
    public async Task HealthAndSensorHandlersApplyEstimatorAndVibrationState()
    {
        var (session, registry, eventHub) = CreateSession();
        var health = new HealthTelemetryHandler(registry, eventHub);
        var sensor = new SensorTelemetryHandler(registry, eventHub);
        await health.HandleAsync(new AhrsMessage(1, 1, EndPoint, 0.1f, 0.2f, 0.3f, 1, 1, 0.4f, 0.5f, ObservedAt), TestContext.Current.CancellationToken);
        await health.HandleAsync(new Ahrs3Message(1, 1, EndPoint, 1, 2, 3, 100, 550_000_000, 120_000_000, 0, 0, 0, 0, ObservedAt), TestContext.Current.CancellationToken);
        await sensor.HandleAsync(new VibrationMessage(1, 1, EndPoint, 1, 4, 5, 6, 7, 8, 9, ObservedAt), TestContext.Current.CancellationToken);

        Assert.Equal(0.4, session.State.Estimator.RollPitchError!.Value, 5);
        Assert.Equal(2, session.State.Estimator.Instance);
        Assert.Equal(55, session.State.Estimator.LatitudeDegrees);
        Assert.Equal([7u, 8u, 9u], session.State.Vibration.Clipping);
    }

    /// <summary>Verifies high-rate raw sensor and obstacle packets stay out of aggregate EventHub handlers.</summary>
    [Fact]
    public void HighRateRawFamiliesRemainTypedDiagnostics()
    {
        var (_, registry, eventHub) = CreateSession();
        var handled = new HashSet<Type>(new SensorTelemetryHandler(registry, eventHub).MessageTypes);
        Type[] diagnosticTypes = [typeof(RawImuMessage), typeof(ScaledImuMessage), typeof(ScaledImu2Message), typeof(ScaledImu3Message), typeof(HighresImuMessage), typeof(ObstacleDistanceMessage), typeof(OpticalFlowMessage), typeof(OpticalFlowRadMessage), typeof(OdometryMessage)];
        Assert.DoesNotContain(diagnosticTypes, handled.Contains);
    }

    /// <summary>Verifies representative sensor traffic traverses the normal message pump.</summary>
    [Fact]
    public async Task MessagePumpDispatchesRepresentativeSensorTraffic()
    {
        var (session, registry, domainEventHub) = CreateSession();
        IVehicleMessageHandler[] handlers = [new SensorTelemetryHandler(registry, domainEventHub)];
        var eventHub = Substitute.For<IEventHub>();
        Func<MavLinkMessage, CancellationToken, Task>? callback = null;
        eventHub.SubscribeAsync(MavLinkEventTopics.ReceivedMessage, Arg.Any<Func<MavLinkMessage, CancellationToken, Task>>()).Returns(call => { callback = call.Arg<Func<MavLinkMessage, CancellationToken, Task>>(); return Substitute.For<IDisposable>(); });
        await using var pump = new VehicleMessagePump(new VehicleMessageDispatcher(handlers), eventHub, NullLogger<VehicleMessagePump>.Instance);
        await pump.StartAsync(TestContext.Current.CancellationToken);
        await callback!(new VibrationMessage(1, 1, EndPoint, 1, 1, 2, 3, 0, 0, 0, ObservedAt), TestContext.Current.CancellationToken);
        Assert.Equal(2, session.State.Vibration.Y);
    }

    private static (VehicleSession Session, IVehicleRegistry Registry, IDomainEventHub EventHub) CreateSession()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ObservedAt);
        var state = new VehicleState(new VehicleId(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, ObservedAt, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
        var session = new VehicleSession(state, EndPoint, clock);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(session.Id).Returns(session);
        registry.Vehicles.Returns([session]);
        return (session, registry, Substitute.For<IDomainEventHub>());
    }
}
