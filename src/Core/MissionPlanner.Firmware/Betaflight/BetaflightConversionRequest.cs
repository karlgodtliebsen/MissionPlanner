using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Confirmed source and explicitly selected catalogue release for conversion.</summary>
public sealed record BetaflightConversionRequest(SerialDeviceDescriptor Source, FirmwareManifestEntry Firmware,
    bool ConfigurationBackupConfirmed, bool PropellersRemovedConfirmed);