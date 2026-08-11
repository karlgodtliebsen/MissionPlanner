namespace MissionPlanner.Core.FlightData.Payload;

/// <summary>Describes conservative camera capabilities.</summary>
public sealed record CameraCapabilities(bool ImageCapture, bool VideoCapture, bool Zoom, bool Focus);
