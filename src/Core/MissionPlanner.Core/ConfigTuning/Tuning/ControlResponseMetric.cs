using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Captures read-only controller response context from PID_TUNING telemetry.</summary>
/// <param name="VehicleId">The source vehicle.</param>
/// <param name="Axis">The protocol axis identifier.</param>
/// <param name="Desired">The desired response.</param>
/// <param name="Achieved">The achieved response.</param>
/// <param name="Error">Desired minus achieved.</param>
/// <param name="FeedForward">The feed-forward contribution.</param>
/// <param name="Proportional">The proportional contribution.</param>
/// <param name="Integral">The integral contribution.</param>
/// <param name="Derivative">The derivative contribution.</param>
/// <param name="ReceivedAt">The reception time.</param>
public sealed record ControlResponseMetric(
    VehicleId VehicleId,
    byte Axis,
    float Desired,
    float Achieved,
    float Error,
    float FeedForward,
    float Proportional,
    float Integral,
    float Derivative,
    DateTimeOffset ReceivedAt);
