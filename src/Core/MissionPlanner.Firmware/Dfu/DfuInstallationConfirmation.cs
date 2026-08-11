namespace MissionPlanner.Firmware.Dfu;

/// <summary>Repeats exact DFU target, device, artifact, and safety evidence at final confirmation.</summary>
public sealed record DfuInstallationConfirmation(
    string Platform,
    int? BoardId,
    DfuDeviceDescriptor Device,
    DfuDeviceInformation DeviceInformation,
    DfuArtifact Artifact,
    DfuTargetSafetyResult Safety);
