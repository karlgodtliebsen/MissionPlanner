using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers.Abstractions;
using MissionPlanner.Core.Vehicles.Observations;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;

namespace MissionPlanner.Core.Vehicles.Handlers;

/// <summary>Promotes low-rate sensor, range, environment, and clock telemetry.</summary>
public sealed class SensorTelemetryHandler(IVehicleRegistry vehicleRegistry, IDomainEventHub domainEventHub)
    : VehicleTelemetryHandlerBase(vehicleRegistry, domainEventHub), IVehicleMessageHandler
{
    /// <summary>Gets the supported low-rate sensor message types.</summary>
    public IReadOnlyCollection<Type> MessageTypes { get; } =
    [
        typeof(VibrationMessage),
        typeof(ScaledPressureMessage),
        typeof(ScaledPressure2Message),
        typeof(ScaledPressure3Message),
        typeof(DistanceSensorMessage),
        typeof(TerrainReportMessage),
        typeof(WindMessage),
        typeof(WindCovMessage),
        typeof(AltitudeMessage),
        typeof(SystemTimeMessage)
    ];

    /// <summary>Converts a supported wire message and applies it to the vehicle session.</summary>
    public async ValueTask HandleAsync(MavLinkMessage message, CancellationToken cancellationToken)
    {
        var vehicle = GetVehicle(message);
        if (vehicle is null) return;
        var previous = vehicle.State;

        switch (message)
        {
            case VibrationMessage vibration:
                vehicle.ApplyVibration(new VehicleVibrationObservation(vibration.VibrationX, vibration.VibrationY, vibration.VibrationZ, [vibration.Clipping0, vibration.Clipping1, vibration.Clipping2], vibration.ReceivedAt));
                break;
            case ScaledPressureMessage pressure:
                ApplyPressure(vehicle, 0, pressure.PressureAbsolute, pressure.PressureDifferential, pressure.Temperature, pressure.DifferentialTemperature, pressure.ReceivedAt);
                break;
            case ScaledPressure2Message pressure:
                ApplyPressure(vehicle, 1, pressure.PressAbs, pressure.PressDiff, pressure.Temperature, pressure.TemperaturePressDiff, pressure.ReceivedAt);
                break;
            case ScaledPressure3Message pressure:
                ApplyPressure(vehicle, 2, pressure.PressAbs, pressure.PressDiff, pressure.Temperature, pressure.TemperaturePressDiff, pressure.ReceivedAt);
                break;
            case DistanceSensorMessage range:
                double? distance = range.CurrentDistance is 0 or ushort.MaxValue ? null : range.CurrentDistance / 100.0;
                vehicle.ApplyRange(new VehicleRangeObservation(range.Id, distance, range.MinDistance / 100.0, range.MaxDistance / 100.0, range.Orientation, range.SignalQuality == byte.MaxValue ? null : range.SignalQuality, range.ReceivedAt));
                break;
            case TerrainReportMessage terrain:
                vehicle.ApplyTerrain(new VehicleTerrainObservation(terrain.TerrainHeight, terrain.CurrentHeight, terrain.ReceivedAt));
                break;
            case WindMessage wind:
                var radians = wind.Direction * Math.PI / 180.0;
                vehicle.ApplyWind(new VehicleWindObservation(wind.Speed * Math.Cos(radians), wind.Speed * Math.Sin(radians), wind.SpeedZ, null, null, wind.ReceivedAt));
                break;
            case WindCovMessage wind:
                vehicle.ApplyWind(new VehicleWindObservation(Finite(wind.WindX), Finite(wind.WindY), Finite(wind.WindZ), Finite(wind.VarHoriz), Finite(wind.VarVert), wind.ReceivedAt));
                break;
            case AltitudeMessage altitude:
                vehicle.ApplyAltitude(new VehicleAltitudeObservation(Finite(altitude.AltitudeMonotonic), Finite(altitude.AltitudeAmsl), Finite(altitude.AltitudeLocal), Finite(altitude.AltitudeRelative), Finite(altitude.AltitudeTerrain), Finite(altitude.BottomClearance), altitude.ReceivedAt));
                break;
            case SystemTimeMessage time:
                vehicle.ApplyTime(new VehicleTimeObservation(ToUnixTime(time.TimeUnixUsec), TimeSpan.FromMilliseconds(time.TimeBootMs), time.ReceivedAt));
                break;
            default:
                throw new ArgumentException("Unsupported message type.", nameof(message));
        }

        await PublishStateIfChangedAsync(previous, vehicle, cancellationToken).ConfigureAwait(false);
    }

    private static void ApplyPressure(VehicleSession vehicle, int instance, float absolute, float differential, short temperature, short? differentialTemperature, DateTimeOffset observedAt)
    {
        vehicle.ApplyPressure(new VehiclePressureObservation(instance, absolute, differential, temperature / 100.0, differentialTemperature / 100.0, observedAt));
    }

    private static double? Finite(float value) => float.IsFinite(value) ? value : null;

    private static DateTimeOffset? ToUnixTime(ulong microseconds)
    {
        if (microseconds == 0 || microseconds > (ulong)((DateTimeOffset.MaxValue - DateTimeOffset.UnixEpoch).Ticks / 10)) return null;
        return DateTimeOffset.UnixEpoch.AddTicks((long)microseconds * 10);
    }
}
