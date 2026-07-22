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
    public IReadOnlyCollection<Type> MessageTypes { get; } = [typeof(EkfStatusReportMessage), typeof(AhrsMessage), typeof(Ahrs3Message)];

    /// <summary>
    /// Provides the public API for HandleAsync.
    /// </summary>
    public async ValueTask HandleAsync(MavLinkMessage message, CancellationToken cancellationToken)
    {
        var vehicle = GetVehicle(message);
        if (vehicle is null)
        {
            return;
        }
        var previous = vehicle.State;

        switch (message)
        {
            case EkfStatusReportMessage ekf:
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
                break;
            case AhrsMessage ahrs:
                vehicle.ApplyEstimatorDiagnostic(new VehicleEstimatorDiagnosticObservation(ahrs.Omegaix, ahrs.Omegaiy, ahrs.Omegaiz, ahrs.ErrorRp, ahrs.ErrorYaw, ahrs.ReceivedAt));
                break;
            case Ahrs3Message ahrs3:
                vehicle.ApplyEstimatorPose(new VehicleEstimatorPoseObservation(2, ahrs3.Roll, ahrs3.Pitch, ahrs3.Yaw, ahrs3.Lat / 10_000_000.0, ahrs3.Lng / 10_000_000.0, ahrs3.Altitude, ahrs3.ReceivedAt));
                break;
            default:
                throw new ArgumentException("Unsupported message type.", nameof(message));
        }

        await PublishStateIfChangedAsync(previous, vehicle, cancellationToken).ConfigureAwait(false);
    }

    // Conservative initial rule: flags == 0 means no estimator capability/health flags.
    // Replace with named EKF flag semantics when those enums are introduced.
    private static bool IsEkfHealthy(ushort flags)
    {
        return flags != 0;
    }
}
