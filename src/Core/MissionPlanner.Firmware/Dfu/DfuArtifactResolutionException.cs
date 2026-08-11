namespace MissionPlanner.Firmware.Dfu;

/// <summary>Indicates that a requested DFU artifact could not be safely resolved.</summary>
public sealed class DfuArtifactResolutionException(string message, Exception? innerException = null) : Exception(message, innerException);
