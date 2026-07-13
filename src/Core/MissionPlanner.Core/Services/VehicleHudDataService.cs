using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.Core.Services;

/// <summary>
/// Service that transforms vehicle state into HUD-specific data format.
/// Subscribes to vehicle state updates and provides reactive streams for UI binding.
/// </summary>
public sealed class VehicleHudDataService : IVehicleHudDataService, IDisposable
{
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IDomainEventHub eventHub;
    private readonly ConcurrentDictionary<VehicleId, VehicleHudData> hudDataCache;
    private readonly Subject<VehicleHudData> hudDataSubject;
    private readonly IDisposable eventSubscription;
    private VehicleId? primaryVehicleId;
    private readonly ILogger<VehicleHudDataService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VehicleHudDataService"/> class.
    /// </summary>
    /// <param name="vehicleRegistry">The vehicle registry to query current vehicle state.</param>
    /// <param name="eventHub">The domain event hub to subscribe to vehicle updates.</param>
    /// <param name="logger">The logger instance.</param>
    public VehicleHudDataService(IVehicleRegistry vehicleRegistry, IDomainEventHub eventHub, ILogger<VehicleHudDataService> logger)
    {
        this.vehicleRegistry = vehicleRegistry ?? throw new ArgumentNullException(nameof(vehicleRegistry));
        this.eventHub = eventHub ?? throw new ArgumentNullException(nameof(eventHub));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        hudDataCache = new ConcurrentDictionary<VehicleId, VehicleHudData>();
        hudDataSubject = new Subject<VehicleHudData>();

        // Subscribe to vehicle state updates
        eventSubscription = this.eventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated);
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
        var vehicleSession = vehicleRegistry.GetRequired(evt.VehicleId);
        if (vehicleSession == null)
        {
            return Task.CompletedTask;
        }

        logger.LogDebug("VehicleHudDataService-Vehicle state updated for {VehicleId}: {State}", evt.VehicleId, vehicleSession.State);
        var hudData = TransformToHudData(vehicleSession.State);
        hudDataCache.AddOrUpdate(evt.VehicleId, hudData, (_, _) => hudData);

        // Publish the update to observers
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
        var pitchDegrees = state.Motion.PitchRadians is { } pitch
            ? pitch * RadiansToDegrees
            : 0.0;

        var rollDegrees = state.Motion.RollRadians is { } roll
            ? roll * RadiansToDegrees
            : 0.0;

        var yawDegrees = state.Motion.YawRadians is { } yaw
            ? yaw * RadiansToDegrees
            : 0.0;

        // GLOBAL_POSITION_INT or similar navigation data normally provides the
        // best heading for the HUD. Fall back to yaw when heading is unavailable.
        var headingDegrees = state.Position.HeadingDegrees ?? NormalizeHeading(yawDegrees);

        var groundSpeed = ValueOrZero(state.Motion.GroundSpeedMetersPerSecond);
        var airSpeed = ValueOrZero(state.Motion.AirSpeedMetersPerSecond);
        var verticalSpeed = ValueOrZero(state.Motion.VerticalSpeedMetersPerSecond);

        // A HUD normally shows altitude relative to the takeoff/home position.
        // Fall back to mean-sea-level altitude when relative altitude is unavailable.
        var altitude = ValueOrZero(state.Position.RelativeAltitudeMeters ?? state.Position.AltitudeMslMeters);

        var batteryVoltage = ValueOrZero(state.Power.BatteryVoltageVolts);
        var batteryRemaining = ValueOrZero(state.Power.BatteryRemainingPercent);

        var gpsSatellites = ValueOrZero(state.Gps.SatellitesVisible);

        var distanceToMav = 0.0; //ValueOrZero(state.Position.DistanceToMav);
        var distanceToWp = 0.0; //ValueOrZero(state.Position.DistanceToWp);

        return new VehicleHudData(
            state.VehicleId,
            pitchDegrees,
            rollDegrees,
            headingDegrees,
            yawDegrees,
            airSpeed,
            groundSpeed,
            altitude,
            verticalSpeed,
            batteryVoltage,
            batteryRemaining,
            distanceToMav,
            distanceToWp,
            (int)gpsSatellites,
            state.Flight.IsArmed,
            state.Flight.Mode,
            state.Position.LatitudeDegrees,
            state.Position.LongitudeDegrees);
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
