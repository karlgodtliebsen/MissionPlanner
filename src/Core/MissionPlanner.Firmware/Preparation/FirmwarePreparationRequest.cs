using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Preparation;

/// <summary>Defines a non-destructive firmware download and validation request.</summary>
public sealed record FirmwarePreparationRequest(FirmwareManifestEntry ManifestEntry);
