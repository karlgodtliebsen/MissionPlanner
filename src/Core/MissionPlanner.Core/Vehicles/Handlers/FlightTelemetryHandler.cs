using MissionPlanner.Core.Models;
using MissionPlanner.Core.Models.Observations;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.VehicleHandler.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.VehicleHandler;

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
        typeof(AttitudeMessage),
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
            var result = await vehicleRegistry.RegisterOrUpdateHeartbeatAsync(
                new VehicleId(heartbeat.SystemId, heartbeat.ComponentId),
                heartbeat.EndPoint,
                heartbeat.CustomMode,
                heartbeat.VehicleType,
                heartbeat.Autopilot,
                heartbeat.BaseMode,
                heartbeat.SystemStatus,
                heartbeat.MavLinkVersion,
                heartbeat.ReceivedAt);

            await PublishStateAsync(result.Vehicle, cancellationToken).ConfigureAwait(false);
            return;
        }

        var vehicle = GetVehicle(message);
        if (vehicle is null)
        {
            return;
        }

        switch (message)
        {
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

        await PublishStateAsync(vehicle, cancellationToken).ConfigureAwait(false);
    }

    private static double NormalizeHeading(short heading)
    {
        var value = heading % 360;
        return value < 0 ? value + 360 : value;
    }
}
