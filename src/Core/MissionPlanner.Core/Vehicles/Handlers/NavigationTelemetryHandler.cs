using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Core.Vehicles.Observations;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers;

/// <summary>
/// Provides the public API for NavigationTelemetryHandler.
/// </summary>
public sealed class NavigationTelemetryHandler(
    IVehicleRegistry vehicleRegistry,
    IDomainEventHub domainEventHub)
    : VehicleTelemetryHandlerBase(vehicleRegistry, domainEventHub), IVehicleMessageHandler
{
    /// <summary>
    /// Provides the public API for MessageTypes.
    /// </summary>
    public IReadOnlyCollection<Type> MessageTypes { get; } =
    [
        typeof(GlobalPositionIntMessage),
        typeof(GpsRawIntMessage),
        typeof(Gps2RawMessage),
        typeof(LocalPositionNedMessage),
        typeof(NavControllerOutputMessage),
        typeof(MissionCurrentMessage),
        typeof(HomePositionMessage)
    ];

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
            case GlobalPositionIntMessage position:
                vehicle.ApplyGlobalPosition(
                    new VehicleGlobalPositionObservation(
                        position.LatitudeDegrees,
                        position.LongitudeDegrees,
                        position.AltitudeMslMeters,
                        position.RelativeAltitudeMeters,
                        position.VelocityNorthMetersPerSecond,
                        position.VelocityEastMetersPerSecond,
                        position.VelocityDownMetersPerSecond,
                        position.HeadingDegrees,
                        position.ReceivedAt));
                break;

            case GpsRawIntMessage gps:
                vehicle.ApplyGps(new VehicleGpsObservation(
                    MapFixType(gps.FixType),
                    gps.SatellitesVisible == byte.MaxValue ? null : gps.SatellitesVisible,
                    gps.Eph == ushort.MaxValue ? null : gps.Eph / 100.0,
                    gps.Epv == ushort.MaxValue ? null : gps.Epv / 100.0,
                    gps.Velocity == ushort.MaxValue ? null : gps.Velocity / 100.0,
                    gps.CourseOverGround == ushort.MaxValue ? null : gps.CourseOverGround / 100.0,
                    gps.HorizontalAccuracy is null or uint.MaxValue ? null : gps.HorizontalAccuracy / 1000.0,
                    gps.VerticalAccuracy is null or uint.MaxValue ? null : gps.VerticalAccuracy / 1000.0,
                    gps.ReceivedAt));
                break;

            case Gps2RawMessage gps2:
                vehicle.ApplyGps(new VehicleGpsObservation(
                    MapFixType(gps2.FixType),
                    gps2.SatellitesVisible == byte.MaxValue ? null : gps2.SatellitesVisible,
                    gps2.Eph == ushort.MaxValue ? null : gps2.Eph / 100.0,
                    gps2.Epv == ushort.MaxValue ? null : gps2.Epv / 100.0,
                    gps2.Vel == ushort.MaxValue ? null : gps2.Vel / 100.0,
                    gps2.Cog == ushort.MaxValue ? null : gps2.Cog / 100.0,
                    gps2.HAcc == uint.MaxValue ? null : gps2.HAcc / 1000.0,
                    gps2.VAcc == uint.MaxValue ? null : gps2.VAcc / 1000.0,
                    gps2.ReceivedAt,
                    1));
                break;

            case LocalPositionNedMessage local:
                vehicle.ApplyLocalPosition(new VehicleLocalPositionObservation(
                    local.X,
                    local.Y,
                    local.Z,
                    local.Vx,
                    local.Vy,
                    local.Vz,
                    local.ReceivedAt));
                break;

            case NavControllerOutputMessage nav:
                vehicle.ApplyNavigation(new VehicleNavigationObservation(
                    nav.NavRoll,
                    nav.NavPitch,
                    nav.NavBearing,
                    nav.TargetBearing,
                    nav.DistanceToWaypoint,
                    nav.AltitudeError,
                    nav.AirspeedError,
                    nav.CrosstrackError,
                    nav.ReceivedAt));
                break;

            case MissionCurrentMessage mission:
                vehicle.ApplyMissionProgress(new VehicleMissionProgressObservation(
                    mission.Sequence,
                    mission.Total,
                    mission.MissionState,
                    mission.MissionMode,
                    mission.ReceivedAt));
                break;

            case HomePositionMessage home:
                vehicle.ApplyHomePosition(new VehicleHomePositionObservation(
                    home.LatitudeDegrees,
                    home.LongitudeDegrees,
                    home.AltitudeMslMeters,
                    home.ReceivedAt));
                break;
        }

        await PublishStateIfChangedAsync(previous, vehicle, cancellationToken).ConfigureAwait(false);
    }

    private static GpsFixType MapFixType(byte value)
    {
        return value switch
        {
            0 => GpsFixType.Unknown,
            1 => GpsFixType.NoGps,
            2 => GpsFixType.NoFix,
            3 => GpsFixType.Fix2D,
            4 => GpsFixType.Fix3D,
            5 => GpsFixType.DifferentialGps,
            6 => GpsFixType.RtkFloat,
            7 => GpsFixType.RtkFixed,
            8 => GpsFixType.Static,
            9 => GpsFixType.Ppp,
            var _ => GpsFixType.Unknown
        };
    }
}
