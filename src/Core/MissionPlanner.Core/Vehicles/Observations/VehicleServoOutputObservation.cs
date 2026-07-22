using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>Represents one bank of raw servo PWM outputs.</summary>
/// <param name="Port">The output bank identifier.</param>
/// <param name="OutputsMicroseconds">The raw PWM output widths in microseconds.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleServoOutputObservation(byte Port, IReadOnlyList<ushort> OutputsMicroseconds, DateTimeOffset ObservedAt) : IVehicleObservation;
