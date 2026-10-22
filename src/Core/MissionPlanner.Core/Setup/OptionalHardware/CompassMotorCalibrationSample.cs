namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>One CompassMot measurement.</summary>
/// <param name="ThrottlePercent">The motor throttle in percent.</param>
/// <param name="CurrentAmps">The measured battery current.</param>
/// <param name="InterferencePercent">The magnetic interference as a percentage of the earth field.</param>
/// <param name="CompensationX">The X compensation value.</param>
/// <param name="CompensationY">The Y compensation value.</param>
/// <param name="CompensationZ">The Z compensation value.</param>
/// <param name="Timestamp">When the measurement was taken.</param>
public sealed record CompassMotorCalibrationSample(double ThrottlePercent, double CurrentAmps, double InterferencePercent, double CompensationX, double CompensationY, double CompensationZ, DateTimeOffset Timestamp);