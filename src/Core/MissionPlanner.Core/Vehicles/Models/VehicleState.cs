namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Immutable snapshot of the domain state known for a vehicle.
/// </summary>
public sealed record VehicleState(
    VehicleId VehicleId,
    VehicleIdentityState Identity,
    VehicleConnectionData Connection,
    VehicleFlightState Flight,
    VehiclePositionState Position,
    VehicleMotionState Motion,
    VehicleGpsState Gps,
    VehiclePowerState Power,
    VehicleRadioState Radio,
    VehicleNavigationState Navigation,
    VehicleHealthState Health)
{
    /// <summary>
    /// Gets the derived user-facing vehicle name.
    /// </summary>
    public string DisplayName => VehicleDisplayNameFormatter.Format(VehicleId, Identity.Firmware.Family, Identity.VehicleType);

    /// <summary>
    /// Gets the CustomMode.
    /// </summary>
    public uint CustomMode => Flight.CustomMode;

    /// <summary>
    /// Gets the VehicleType.
    /// </summary>
    public byte VehicleType => Identity.VehicleType;

    /// <summary>
    /// Gets the Autopilot.
    /// </summary>
    public byte Autopilot => Identity.Autopilot;

    /// <summary>
    /// Gets the BaseMode.
    /// </summary>
    public byte BaseMode => Flight.BaseMode;

    /// <summary>
    /// Gets the SystemStatus.
    /// </summary>
    public byte SystemStatus => Flight.SystemStatus;

    /// <summary>
    /// Gets the MavLinkVersion.
    /// </summary>
    public byte MavLinkVersion => Identity.MavLinkVersion;

    /// <summary>
    /// Gets the ConnectionState.
    /// </summary>
    public VehicleConnectionState ConnectionState => Connection.State;

    /// <summary>
    /// The timestamp of the last received heartbeat from the vehicle.
    /// </summary>
    public DateTimeOffset LastHeartbeatAt => Connection.LastHeartbeatAt;

    /// <summary>
    /// The current mode of the vehicle, as reported by the flight controller.
    /// </summary>
    public VehicleMode Mode => Flight.Mode;

    /// <summary>
    /// Whether the vehicle is armed.
    /// </summary>
    public bool IsArmed => Flight.IsArmed;

    /// <summary>
    /// Gets the latitude of the vehicle in degrees.
    /// </summary>
    public double? Latitude => Position.LatitudeDegrees;

    /// <summary>
    /// Gets the longitude of the vehicle in degrees.
    /// </summary>
    public double? Longitude => Position.LongitudeDegrees;

    /// <summary>
    /// Gets the altitude of the vehicle above mean sea level in meters.
    /// </summary>
    public double? Altitude => Position.AltitudeMslMeters;

    /// <summary>
    /// Gets the roll angle of the vehicle in radians.
    /// </summary>
    public double? Roll => Motion.RollRadians;

    /// <summary>
    /// Gets the pitch angle of the vehicle in radians.
    /// </summary>
    public double? Pitch => Motion.PitchRadians;

    /// <summary>
    /// Gets the yaw angle of the vehicle in radians.
    /// </summary>
    public double? Yaw => Motion.YawRadians;

    /// <summary>
    /// Gets the remaining battery percentage of the vehicle.
    /// </summary>
    public int? BatteryRemaining => Power.BatteryRemainingPercent;

    /// <summary>
    /// Gets the battery voltage of the vehicle in volts.
    /// </summary>
    public float? BatteryVoltage => Power.BatteryVoltageVolts is { } value ? (float)value : null;


    /// <summary>
    /// Compatibility constructor matching the previous flat state record.
    /// </summary>
    public VehicleState(
        VehicleId vehicleId,
        uint customMode,
        byte vehicleType,
        byte autopilot,
        byte baseMode,
        byte systemStatus,
        byte mavLinkVersion,
        VehicleConnectionState connectionState,
        DateTimeOffset lastHeartbeatAt,
        VehicleMode mode,
        bool isArmed,
        double? latitude,
        double? longitude,
        double? altitude,
        double? roll,
        double? pitch,
        double? yaw,
        int? batteryRemaining,
        float? batteryVoltage)
        : this(
            vehicleId,
            new VehicleIdentityState(vehicleType, autopilot, mavLinkVersion, VehicleFirmwareIdentityFactory.FromHeartbeat(vehicleType, autopilot)),
            new VehicleConnectionData(connectionState, lastHeartbeatAt),
            new VehicleFlightState(customMode, baseMode, systemStatus, mode, isArmed),
            VehiclePositionState.Empty with { LatitudeDegrees = latitude, LongitudeDegrees = longitude, AltitudeMslMeters = altitude },
            VehicleMotionState.Empty with { RollRadians = roll, PitchRadians = pitch, YawRadians = yaw },
            VehicleGpsState.Empty,
            VehiclePowerState.Empty with { BatteryRemainingPercent = batteryRemaining, BatteryVoltageVolts = batteryVoltage },
            VehicleRadioState.Empty,
            VehicleNavigationState.Empty,
            VehicleHealthState.Empty)
    {
    }
}
