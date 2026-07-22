namespace MissionPlanner.Core.Setup;

/// <summary>Contains the vehicle-specific evaluation of a setup workflow.</summary>
/// <param name="Descriptor">The workflow definition.</param>
/// <param name="State">The current workflow state.</param>
/// <param name="StatusText">A user-facing state explanation.</param>
/// <param name="IsVisible">Whether the workflow is relevant enough to show.</param>
public sealed record SetupWorkflowEvaluation(
    SetupWorkflowDescriptor Descriptor,
    SetupWorkflowState State,
    string StatusText,
    bool IsVisible);
