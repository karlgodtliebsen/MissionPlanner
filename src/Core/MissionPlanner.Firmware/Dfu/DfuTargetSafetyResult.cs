namespace MissionPlanner.Firmware.Dfu;

/// <summary>Contains a target-safety decision, evidence codes, and any required typed phrase.</summary>
public sealed record DfuTargetSafetyResult(
    DfuTargetSafetyDecision Decision,
    IReadOnlyList<string> EvidenceCodes,
    string? RequiredConfirmationPhrase = null);
