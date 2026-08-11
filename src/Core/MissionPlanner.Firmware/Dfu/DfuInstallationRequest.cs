namespace MissionPlanner.Firmware.Dfu;

/// <summary>Requests the complete DFU installation use case.</summary>
public sealed record DfuInstallationRequest(
    string SelectedPlatform,
    int? SelectedBoardId,
    DfuDeviceDescriptor Device,
    DfuArtifact? Artifact = null,
    Uri? ArtifactSource = null,
    string? ConfirmationPhrase = null,
    Model.FirmwareManifestEntry? ManifestEntry = null,
    string? LocalHexPath = null,
    Model.SerialDeviceDescriptor? PreviousApplicationDevice = null,
    DfuRememberedAssociation? RememberedAssociation = null);
