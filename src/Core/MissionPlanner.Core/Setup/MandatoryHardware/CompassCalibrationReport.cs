namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Projects a completed per-compass calibration report.</summary>
/// <param name="CompassId">The zero-based compass identifier.</param>
/// <param name="Success">Whether the compass calibrated successfully.</param>
/// <param name="Autosaved">Whether the vehicle already saved the result.</param>
/// <param name="Fitness">The reported RMS milligauss residual (lower is better).</param>
/// <param name="OffsetX">The computed X offset.</param>
/// <param name="OffsetY">The computed Y offset.</param>
/// <param name="OffsetZ">The computed Z offset.</param>
/// <param name="OldOrientation">The orientation before calibration.</param>
/// <param name="NewOrientation">The orientation after calibration.</param>
/// <param name="OrientationConfidence">The reported orientation confidence.</param>
public sealed record CompassCalibrationReport(
    int CompassId,
    bool Success,
    bool Autosaved,
    double Fitness,
    double OffsetX,
    double OffsetY,
    double OffsetZ,
    int OldOrientation,
    int NewOrientation,
    double OrientationConfidence);
