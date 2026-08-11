namespace MissionPlanner.Firmware.Dfu;

/// <summary>Contains a provider or orchestration result.</summary>
public sealed record DfuProgrammingResult(
    DfuOperationState State,
    bool ProgrammingSucceeded,
    bool VerificationSucceeded,
    bool ApplicationRediscovered,
    DfuFailure? Failure = null,
    string? ProviderLog = null,
    int? ExitCode = null,
    DfuProgrammingOutcome Outcome = DfuProgrammingOutcome.ProgrammingFailed,
    Guid? OperationId = null,
    IReadOnlyList<string>? Warnings = null);
