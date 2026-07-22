using MissionPlanner.Core.Setup;

namespace MissionPlanner.App.Views.InitSetup;

/// <summary>Represents one vehicle-evaluated workflow card in the Setup shell.</summary>
public sealed class SetupWorkflowItemViewModel
{
    /// <summary>Initializes a workflow card.</summary>
    /// <param name="evaluation">The evaluated workflow.</param>
    public SetupWorkflowItemViewModel(SetupWorkflowEvaluation evaluation) => Evaluation = evaluation;

    /// <summary>Gets the underlying evaluation.</summary>
    public SetupWorkflowEvaluation Evaluation { get; }

    /// <summary>Gets the workflow definition.</summary>
    public SetupWorkflowDescriptor Descriptor => Evaluation.Descriptor;

    /// <summary>Gets the workflow title.</summary>
    public string Title => Descriptor.Title;

    /// <summary>Gets the workflow description.</summary>
    public string Description => Descriptor.Description;

    /// <summary>Gets the evaluated state.</summary>
    public SetupWorkflowState State => Evaluation.State;

    /// <summary>Gets the state explanation.</summary>
    public string StatusText => Evaluation.StatusText;

    /// <summary>Gets a non-color state marker and label.</summary>
    public string StateDisplay => State switch
    {
        SetupWorkflowState.Available => "○ AVAILABLE",
        SetupWorkflowState.Unsupported => "— UNSUPPORTED",
        SetupWorkflowState.NotConnected => "◇ NOT CONNECTED",
        SetupWorkflowState.NotStarted => "○ NOT STARTED",
        SetupWorkflowState.InProgress => "… IN PROGRESS",
        SetupWorkflowState.Completed => "✓ COMPLETED",
        SetupWorkflowState.Warning => "⚠ REVIEW",
        _ => "✖ FAILED"
    };

    /// <summary>Gets whether the workflow may be reviewed for the online vehicle.</summary>
    public bool CanOpen => State is not SetupWorkflowState.Unsupported and not SetupWorkflowState.NotConnected;
}
