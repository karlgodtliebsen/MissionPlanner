namespace MissionPlanner.Core.Setup;

/// <summary>Represents locally persisted evidence that a setup workflow was completed.</summary>
/// <param name="VehicleKey">The stable local vehicle identity key.</param>
/// <param name="Workflow">The completed workflow.</param>
/// <param name="FirmwareSignature">The firmware identity at completion.</param>
/// <param name="ParameterSignature">The parameter-set signature at completion.</param>
/// <param name="CompletedAt">The completion timestamp.</param>
public sealed record SetupCompletionEvidence(
    string VehicleKey,
    SetupWorkflowKey Workflow,
    string FirmwareSignature,
    string ParameterSignature,
    DateTimeOffset CompletedAt);
