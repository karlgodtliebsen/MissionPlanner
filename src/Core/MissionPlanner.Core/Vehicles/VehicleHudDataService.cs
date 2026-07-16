using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Service that transforms vehicle state into HUD-specific data format.
/// Subscribes to vehicle state updates and provides reactive streams for UI binding.
/// </summary>
public sealed class VehicleHudDataService : IVehicleHudDataService, IDisposable
{
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IDomainEventHub domainEventHub;
    private readonly ConcurrentDictionary<VehicleId, VehicleHudData> hudDataCache;
    private readonly Subject<VehicleHudData> hudDataSubject;
    private readonly IDisposable eventSubscription;
    private VehicleId? primaryVehicleId;
    private readonly ILogger<VehicleHudDataService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VehicleHudDataService"/> class.
    /// </summary>
    /// <param name="vehicleRegistry">The vehicle registry to query current vehicle state.</param>
    /// <param name="domainEventHub">The domain event hub to subscribe to vehicle updates.</param>
    /// <param name="logger">The logger instance.</param>
    public VehicleHudDataService(IVehicleRegistry vehicleRegistry, IDomainEventHub domainEventHub, ILogger<VehicleHudDataService> logger)
    {
        this.vehicleRegistry = vehicleRegistry ?? throw new ArgumentNullException(nameof(vehicleRegistry));
        this.domainEventHub = domainEventHub ?? throw new ArgumentNullException(nameof(domainEventHub));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        hudDataCache = new ConcurrentDictionary<VehicleId, VehicleHudData>();
        hudDataSubject = new Subject<VehicleHudData>();

        // Subscribe to vehicle state updates
        eventSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated);
    }

    /// <inheritdoc/>
    public VehicleHudData? GetHudData(VehicleId vehicleId)
    {
        if (hudDataCache.TryGetValue(vehicleId, out var cachedData))
        {
            return cachedData;
        }

        // If not in cache, try to get from registry
        var vehicleSession = vehicleRegistry.GetRequired(vehicleId);
        if (vehicleSession == null)
        {
            return null;
        }

        var hudData = TransformToHudData(vehicleSession.State);
        hudDataCache.TryAdd(vehicleId, hudData);
        return hudData;
    }

    /// <inheritdoc/>
    public IObservable<VehicleHudData> ObserveHudData(VehicleId vehicleId)
    {
        return hudDataSubject
            .Where(data => data.VehicleId == vehicleId)
            .AsObservable();
    }

    /// <inheritdoc/>
    public VehicleHudData? GetPrimaryVehicleHudData()
    {
        var vehicleId = GetOrSetPrimaryVehicleId();
        return vehicleId.HasValue ? GetHudData(vehicleId.Value) : null;
    }

    /// <inheritdoc/>
    public IObservable<VehicleHudData> ObservePrimaryVehicleHudData()
    {
        return hudDataSubject
            .Where(data =>
            {
                var primaryId = GetOrSetPrimaryVehicleId();
                return primaryId != null && data.VehicleId == primaryId;
            })
            .AsObservable();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        eventSubscription?.Dispose();
        hudDataSubject?.Dispose();
    }

    private Task OnVehicleStateUpdated(VehicleStateUpdated evt, CancellationToken cancellationToken)
    {
        var state = evt.VehicleState;

        logger.LogDebug("HUD state update for {VehicleId}: {@State}", state.VehicleId, state);

        var hudData = TransformToHudData(state);

        //hudDataCache.AddOrUpdate(state.VehicleId, hudData, static (_, newValue) => newValue, hudData);
        hudDataCache.AddOrUpdate(state.VehicleId, hudData, (_, _) => hudData);

        hudDataSubject.OnNext(hudData);

        return Task.CompletedTask;
    }

    private VehicleId? GetOrSetPrimaryVehicleId()
    {
        if (primaryVehicleId != null)
        {
            return primaryVehicleId;
        }

        // Auto-select the first available vehicle as primary
        var vehicles = vehicleRegistry.Vehicles;
        if (vehicles.Count > 0)
        {
            primaryVehicleId = vehicles.First().Id;
        }

        return primaryVehicleId;
    }

    private const double RadiansToDegrees = 180.0 / Math.PI;

    private static VehicleHudData TransformToHudData(VehicleState state)
    {
        var pitchDegrees = RadiansToDisplayDegrees(state.Motion.PitchRadians);

        var rollDegrees = RadiansToDisplayDegrees(state.Motion.RollRadians);

        var yawDegrees = RadiansToDisplayDegrees(state.Motion.YawRadians);

        var headingDegrees = NormalizeHeading(state.Position.HeadingDegrees ?? yawDegrees);

        var altitude = ValueOrZero(state.Position.RelativeAltitudeMeters ?? state.Position.AltitudeMslMeters);

        var distanceToWp = ValueOrZero(state.Navigation.WaypointDistanceMeters);

        var distanceToMav = CalculateDistanceToMav(state.Position);

        return new VehicleHudData(
            state.VehicleId,
            pitchDegrees,
            rollDegrees,
            headingDegrees,
            yawDegrees,
            ValueOrZero(state.Motion.AirSpeedMetersPerSecond),
            ValueOrZero(state.Motion.GroundSpeedMetersPerSecond),
            altitude,
            ValueOrZero(state.Motion.VerticalSpeedMetersPerSecond),
            ValueOrZero(state.Power.BatteryVoltageVolts),
            ValueOrZero(state.Power.BatteryRemainingPercent),
            distanceToMav,
            distanceToWp,
            state.Gps.SatellitesVisible ?? 0,
            state.Flight.IsArmed,
            state.Flight.Mode,
            state.Position.LatitudeDegrees,
            state.Position.LongitudeDegrees);
    }

    /// <summary>
    /// Distance from the home (launch) position to the vehicle — what the classic
    /// MissionPlanner displays as "DistToMAV" (bound to DistToHome). Zero until both a
    /// home position (HOME_POSITION message) and a vehicle fix are known.
    /// </summary>
    private static double CalculateDistanceToMav(VehiclePositionState position)
    {
        if (position.HomeLatitudeDegrees is not { } homeLat ||
            position.HomeLongitudeDegrees is not { } homeLng ||
            position.LatitudeDegrees is not { } lat ||
            position.LongitudeDegrees is not { } lng ||
            (lat == 0 && lng == 0) ||
            (homeLat == 0 && homeLng == 0))
        {
            return 0.0;
        }

        return GeoMath.ApproximateDistanceMeters(homeLat, homeLng, lat, lng);
    }

    private static double RadiansToDisplayDegrees(double? radians)
    {
        return radians is not { } value || !double.IsFinite(value) ? 0.0 : value * RadiansToDegrees;
    }

    private static double ValueOrZero(double? value)
    {
        return value is { } number && double.IsFinite(number)
            ? number
            : 0.0;
    }

    private static double NormalizeHeading(double degrees)
    {
        var normalized = degrees % 360.0;

        if (normalized < 0.0)
        {
            normalized += 360.0;
        }

        return normalized;
    }
}
