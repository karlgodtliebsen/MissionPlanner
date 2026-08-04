namespace MissionPlanner.Firmware.Dfu;

/// <summary>Identifies a DFU target-safety decision.</summary>
public enum DfuTargetSafetyDecision
{
    /// <summary>Known evidence and a remembered device association support the exact selection.</summary>
    Allowed,
    /// <summary>No known conflict exists, but board identity remains ambiguous.</summary>
    AllowedWithStrongWarning,
    /// <summary>Required evidence is absent or known evidence is incompatible.</summary>
    Blocked
}

/// <summary>Defines known MCU and flash constraints for one exact ArduPilot target.</summary>
public sealed record DfuTargetPolicy(
    string Platform,
    int? BoardId,
    IReadOnlyList<string> CompatibleMcuDeviceIds,
    long? MinimumInternalFlashBytes = null,
    long? MaximumInternalFlashBytes = null);

/// <summary>Records an operator-approved association without inferring it from MCU identity.</summary>
public sealed record DfuRememberedAssociation(
    string Platform,
    int? BoardId,
    string ApplicationIdentity,
    string DfuSerialNumber);

/// <summary>Contains all evidence used to evaluate one explicit DFU target selection.</summary>
public sealed record DfuTargetSafetyRequest(
    string? SelectedPlatform,
    int? SelectedBoardId,
    DfuArtifact? Artifact,
    DfuDeviceInformation? DeviceInformation,
    MissionPlanner.Firmware.Model.FirmwareManifestEntry? ManifestEntry = null,
    string? PreviousApplicationIdentity = null,
    DfuRememberedAssociation? RememberedAssociation = null,
    string? ConfirmationPhrase = null,
    bool IsNormalInstall = true);

/// <summary>Contains a target-safety decision, evidence codes, and any required typed phrase.</summary>
public sealed record DfuTargetSafetyResult(
    DfuTargetSafetyDecision Decision,
    IReadOnlyList<string> EvidenceCodes,
    string? RequiredConfirmationPhrase = null);
