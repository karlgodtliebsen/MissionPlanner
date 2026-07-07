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

    private static VehicleHudData TransformToHudData(VehicleState state)
    {
        // Convert radians to degrees for display
        const double RadiansToDegrees = 180.0 / Math.PI;

        double pitch = state.Pitch.HasValue ? state.Pitch.Value * RadiansToDegrees : 0;
        double roll = state.Roll.HasValue ? state.Roll.Value * RadiansToDegrees : 0;
        double yaw = state.Yaw.HasValue ? state.Yaw.Value * RadiansToDegrees : 0;

        // Convert yaw (-180 to 180 degrees) to heading (0 to 360 degrees)
        var heading = yaw < 0 ? yaw + 360 : yaw;

        // Calculate vertical speed (would need velocity data from GPS or VFR_HUD message)
        // For now, we'll set it to 0 as VehicleState doesn't have this yet
        double verticalSpeed = 0;

        // Calculate ground speed from GPS data (would need velocity components)
        // For now, we'll set it to 0 as VehicleState doesn't have this yet
        double groundSpeed = 0;

        // Air speed would come from VFR_HUD or similar messages
        double airSpeed = 0;

        // GPS satellites count (would need GPS status message)
        var gpsSatellites = 0;

        return new VehicleHudData(
            state.VehicleId,
            pitch,
            roll,
            heading,
            yaw,
            airSpeed,
            groundSpeed,
            state.Altitude ?? 0,
            verticalSpeed,
            state.BatteryVoltage ?? 0,
            state.BatteryRemaining ?? 0,
            gpsSatellites,
            state.IsArmed,
            state.Mode,
            state.Latitude,
            state.Longitude);
    }
}
