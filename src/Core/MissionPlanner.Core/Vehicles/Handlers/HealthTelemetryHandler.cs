using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Core.Vehicles.Observations;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers;

/// <summary>
/// Provides the public API for HealthTelemetryHandler.
/// </summary>
public sealed class HealthTelemetryHandler(
    IVehicleRegistry vehicleRegistry,
    IDomainEventHub domainEventHub)
    : VehicleTelemetryHandlerBase(vehicleRegistry, domainEventHub), IVehicleMessageHandler
{
    /// <summary>
    /// Provides the public API for MessageTypes.
    /// </summary>
    public IReadOnlyCollection<Type> MessageTypes { get; } = [typeof(EkfStatusReportMessage)];

    /// <summary>
    /// Provides the public API for HandleAsync.
    /// </summary>
    public async ValueTask HandleAsync(MavLinkMessage message, CancellationToken cancellationToken)
    {
        if (message is not EkfStatusReportMessage ekf)
        {
            throw new ArgumentException("Unsupported message type.", nameof(message));
        }

        var vehicle = GetVehicle(ekf);
        if (vehicle is null)
        {
            return;
        }

        vehicle.ApplyEkf(new VehicleEkfObservation(
            ekf.Flags,
            IsEkfHealthy(ekf.Flags),
            ekf.VelocityVariance,
            ekf.PositionHorizontalVariance,
            ekf.PositionVerticalVariance,
            ekf.CompassVariance,
            ekf.TerrainAltitudeVariance,
            ekf.AirspeedVariance,
            ekf.ReceivedAt));

        await PublishStateAsync(vehicle, cancellationToken).ConfigureAwait(false);
    }

    // Conservative initial rule: flags == 0 means no estimator capability/health flags.
    // Replace with named EKF flag semantics when those enums are introduced.
    private static bool IsEkfHealthy(ushort flags)
    {
        return flags != 0;
    }
}
