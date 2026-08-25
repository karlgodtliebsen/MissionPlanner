namespace MissionPlanner.Core.Setup.OptionalHardware;

public sealed record CompassMotorCalibrationSample(double ThrottlePercent, double CurrentAmps, double InterferencePercent, double CompensationX, double CompensationY, double CompensationZ, DateTimeOffset Timestamp);