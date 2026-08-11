namespace MissionPlanner.Core.Setup;

/// <summary>Projects the live per-compass calibration progress.</summary>
/// <param name="CompassId">The zero-based compass identifier reported by the vehicle.</param>
/// <param name="Status">The projected calibration status.</param>
/// <param name="CompletionPercent">The completion percentage from zero to one hundred.</param>
/// <param name="Attempt">The attempt number reported by the vehicle.</param>
public sealed record CompassCalibrationProgress(int CompassId, CompassCalibrationStatus Status, int CompletionPercent, int Attempt);
