using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Installation;

/// <summary>Identifies the active normal connection transport.</summary>
public enum ConnectionTransportKind
{
    /// <summary>A serial or USB connection.</summary>
    Serial,
    /// <summary>A TCP connection.</summary>
    Tcp,
    /// <summary>A UDP connection.</summary>
    Udp,
    /// <summary>Another connection kind.</summary>
    Other
}

/// <summary>Defines one disconnected application-firmware installation.</summary>
public sealed record FirmwareInstallationRequest(
    BootloaderEntryContext EntryContext,
    FirmwareArtifact? Artifact = null,
    ApjFirmwarePackage? Package = null);

/// <summary>Repeats the final pre-erase compatibility evidence.</summary>
public sealed record FirmwareInstallationConfirmation(
    int FirmwareBoardId,
    int DetectedBoardId,
    int BootloaderRevision,
    long ImageSize,
    string Source);

/// <summary>Defines a non-dialog manual action request.</summary>
public sealed record FirmwareManualAction(string Code, string? TechnicalDetail = null);
