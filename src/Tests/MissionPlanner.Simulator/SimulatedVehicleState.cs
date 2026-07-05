using MissionPlanner.Core.Models;

namespace MissionPlanner.Simulator;

/// <summary>
/// Represents the state of a simulated vehicle.
/// </summary>
public sealed class SimulatedVehicleState
{
    public VehicleId VehicleId { get; init; } = new(1, 1);

    public uint CustomMode { get; init; } = 0;

    public byte VehicleType { get; init; } = 2;

    public byte Autopilot { get; init; } = 3;

    public byte BaseMode { get; init; } = 0;

    public byte SystemStatus { get; init; } = 4;

    public byte MavLinkVersion { get; init; } = 3;

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public double? Altitude { get; init; }

    public double? Roll { get; init; }

    public double? Pitch { get; init; }

    public double? Yaw { get; init; }

    public int? BatteryRemaining { get; init; }

    public float? BatteryVoltage { get; init; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}