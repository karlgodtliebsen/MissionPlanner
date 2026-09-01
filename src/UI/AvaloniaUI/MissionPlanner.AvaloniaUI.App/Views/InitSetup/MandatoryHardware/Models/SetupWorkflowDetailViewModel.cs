using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup;
using MissionPlanner.AvaloniaUI.App.Utilities;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware.Models;

/// <summary>Provides the common presentation and lifecycle state for one Setup workflow.</summary>
public partial class SetupWorkflowDetailViewModel : ViewModelBase
{
    /// <summary>Initializes a workflow ViewModel.</summary>
    /// <param name="descriptor">The workflow definition.</param>
    /// <param name="logger"></param>
    public SetupWorkflowDetailViewModel(SetupWorkflowDescriptor descriptor, ILogger logger) : base(logger)
    {
        Descriptor = descriptor;
    }

    /// <summary>Gets the workflow definition.</summary>
    public SetupWorkflowDescriptor Descriptor
    {
        get;
    }

    /// <summary>Gets the workflow title.</summary>
    public string Title => Descriptor.Title;

    /// <summary>Gets the workflow purpose.</summary>
    public string Description => Descriptor.Description;

    /// <summary>Gets whether this workflow links to an existing Config page.</summary>
    public bool HasConfigDestination => Descriptor.ConfigDestination is not null;

    /// <summary>Cancels work owned by this workflow ViewModel.</summary>
    public virtual void Cancel()
    {
    }
}

