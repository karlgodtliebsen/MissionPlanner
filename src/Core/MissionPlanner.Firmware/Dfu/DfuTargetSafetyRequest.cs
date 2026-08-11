namespace MissionPlanner.Firmware.Dfu;

/// <summary>Contains all evidence used to evaluate one explicit DFU target selection.</summary>
public sealed record DfuTargetSafetyRequest(
    string? SelectedPlatform,
    int? SelectedBoardId,
    DfuArtifact? Artifact,
    DfuDeviceInformation? DeviceInformation,
    Model.FirmwareManifestEntry? ManifestEntry = null,
    string? PreviousApplicationIdentity = null,
    DfuRememberedAssociation? RememberedAssociation = null,
    string? ConfirmationPhrase = null,
    bool IsNormalInstall = true);
