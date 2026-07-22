using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Core.Vehicles.Observations;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers;

/// <summary>
/// Handles flight telemetry messages and updates the vehicle state accordingly.
/// </summary>
/// <param name="vehicleRegistry">The vehicle registry service.</param>
/// <param name="domainEventHub">The domain event hub service.</param>
public sealed class FlightTelemetryHandler(IVehicleRegistry vehicleRegistry, IDomainEventHub domainEventHub)
    : VehicleTelemetryHandlerBase(vehicleRegistry, domainEventHub), IVehicleMessageHandler
{
    /// <summary>
    /// Gets the types of MAVLink messages that this handler can process.
    /// </summary>
    public IReadOnlyCollection<Type> MessageTypes { get; } =
    [
        typeof(HeartbeatMessage),
        typeof(AutopilotVersionMessage),
        typeof(AttitudeMessage),
        typeof(AttitudeQuaternionMessage),
        typeof(ExtendedSysStateMessage),
        typeof(Ahrs2Message),
        typeof(VfrHudMessage)
    ];

    /// <summary>
    /// Handles incoming MAVLink messages and updates the vehicle state accordingly.
    /// </summary>
    /// <param name="message">The MAVLink message to handle.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public async ValueTask HandleAsync(MavLinkMessage message, CancellationToken cancellationToken)
    {
        // Heartbeat is special because it may create the session.
        if (message is HeartbeatMessage heartbeat)
        {
            var vehicleId = new VehicleId(heartbeat.SystemId, heartbeat.ComponentId);
            var previousState = VehicleRegistry.Vehicles.FirstOrDefault(vehicle => vehicle.Id == vehicleId)?.State;
            var result = await VehicleRegistry.RegisterOrUpdateHeartbeatAsync(
                vehicleId,
                heartbeat.EndPoint,
                heartbeat.CustomMode,
                heartbeat.VehicleType,
                heartbeat.Autopilot,
                heartbeat.BaseMode,
                heartbeat.SystemStatus,
                heartbeat.MavLinkVersion,
                heartbeat.ReceivedAt, cancellationToken);

            if (previousState is null)
            {
                await PublishStateAsync(result.Vehicle, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await PublishStateIfChangedAsync(previousState, result.Vehicle, cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        var vehicle = GetVehicle(message);
        if (vehicle is null)
        {
            return;
        }
        var previous = vehicle.State;

        switch (message)
        {
            case AutopilotVersionMessage version:
                vehicle.ApplyFirmwareIdentity(new VehicleFirmwareObservation(
                    version.Capabilities,
                    version.FlightSoftwareVersion,
                    version.BoardVersion,
                    version.FlightCustomVersion,
                    version.VendorId,
                    version.ProductId,
                    version.Uid,
                    version.Uid2,
                    version.ReceivedAt));
                break;

            case AttitudeMessage attitude:
                vehicle.ApplyAttitude(new VehicleAttitudeObservation(
                    attitude.Roll,
                    attitude.Pitch,
                    attitude.Yaw,
                    null,
                    null,
                    null,
                    attitude.ReceivedAt));
                break;

            case AttitudeQuaternionMessage quaternion:
                var euler = ToEuler(quaternion.Q1, quaternion.Q2, quaternion.Q3, quaternion.Q4);
                vehicle.ApplyAttitude(new VehicleAttitudeObservation(
                    euler.Roll,
                    euler.Pitch,
                    euler.Yaw,
                    quaternion.Rollspeed,
                    quaternion.Pitchspeed,
                    quaternion.Yawspeed,
                    quaternion.ReceivedAt));
                break;

            case ExtendedSysStateMessage extended:
                vehicle.ApplyExtendedFlightState(new VehicleExtendedFlightStateObservation(
                    MapVtolState(extended.VtolState),
                    MapLandedState(extended.LandedState),
                    extended.ReceivedAt));
                break;

            case Ahrs2Message ahrs2:
                vehicle.ApplyAhrsFallback(
                    new VehicleAhrsObservation(
                        ahrs2.Roll,
                        ahrs2.Pitch,
                        ahrs2.Yaw,
                        ahrs2.Latitude,
                        ahrs2.Longitude,
                        ahrs2.Altitude,
                        true,
                        ahrs2.ReceivedAt));
                break;


            case VfrHudMessage hud:
                vehicle.ApplyHud(new VehicleHudObservation(
                    hud.Airspeed,
                    hud.Groundspeed,
                    NormalizeHeading(hud.Heading),
                    hud.Altitude,
                    hud.Climb,
                    hud.ReceivedAt));
                break;
        }

        await PublishStateIfChangedAsync(previous, vehicle, cancellationToken).ConfigureAwait(false);
    }

    private static double NormalizeHeading(short heading)
    {
        var value = heading % 360;
        return value < 0 ? value + 360 : value;
    }

    private static (double Roll, double Pitch, double Yaw) ToEuler(double w, double x, double y, double z)
    {
        var norm = Math.Sqrt((w * w) + (x * x) + (y * y) + (z * z));
        if (norm <= double.Epsilon)
        {
            return (0, 0, 0);
        }

        w /= norm;
        x /= norm;
        y /= norm;
        z /= norm;
        var roll = Math.Atan2(2 * ((w * x) + (y * z)), 1 - (2 * ((x * x) + (y * y))));
        var pitchTerm = Math.Clamp(2 * ((w * y) - (z * x)), -1, 1);
        var pitch = Math.Asin(pitchTerm);
        var yaw = Math.Atan2(2 * ((w * z) + (x * y)), 1 - (2 * ((y * y) + (z * z))));
        return (roll, pitch, yaw);
    }

    private static VehicleVtolState MapVtolState(byte value) => value <= 4 ? (VehicleVtolState)value : VehicleVtolState.Undefined;

    private static VehicleLandedState MapLandedState(byte value) => value <= 4 ? (VehicleLandedState)value : VehicleLandedState.Undefined;
}
