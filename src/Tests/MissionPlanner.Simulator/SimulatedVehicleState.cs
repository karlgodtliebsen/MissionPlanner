using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Simulator;

/// <summary>
/// Represents the state of a simulated vehicle.
/// </summary>
public sealed class SimulatedVehicleState
{
    /// <summary>
    /// Provides the public API for VehicleId.
    /// </summary>
    public VehicleId VehicleId { get; init; } = new(1, 1);

    /// <summary>
    /// Provides the public API for CustomMode.
    /// </summary>
    public uint CustomMode { get; init; } = 0;

    /// <summary>
    /// Provides the public API for VehicleType.
    /// </summary>
    public byte VehicleType { get; init; } = 2;

    /// <summary>
    /// Provides the public API for Autopilot.
    /// </summary>
    public byte Autopilot { get; init; } = 3;

    /// <summary>
    /// Provides the public API for BaseMode.
    /// </summary>
    public byte BaseMode { get; init; } = 0;

    /// <summary>
    /// Provides the public API for SystemStatus.
    /// </summary>
    public byte SystemStatus { get; init; } = 4;

    /// <summary>
    /// Provides the public API for MavLinkVersion.
    /// </summary>
    public byte MavLinkVersion { get; init; } = 3;

    /// <summary>
    /// Provides the public API for Latitude.
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// Provides the public API for Longitude.
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// Provides the public API for Altitude.
    /// </summary>
    public double? Altitude { get; init; }

    /// <summary>
    /// Provides the public API for Roll.
    /// </summary>
    public double? Roll { get; init; }

    /// <summary>
    /// Provides the public API for Pitch.
    /// </summary>
    public double? Pitch { get; init; }

    /// <summary>
    /// Provides the public API for Yaw.
    /// </summary>
    public double? Yaw { get; init; }

    /// <summary>
    /// Provides the public API for BatteryRemaining.
    /// </summary>
    public int? BatteryRemaining { get; init; }

    /// <summary>
    /// Provides the public API for BatteryVoltage.
    /// </summary>
    public float? BatteryVoltage { get; init; }

    /// <summary>
    /// Provides the public API for Timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
