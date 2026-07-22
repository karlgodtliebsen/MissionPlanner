using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.Setup;

namespace MissionPlanner.App.Views.InitSetup.Sections;

/// <summary>Provides the lazily created host model for one Setup workflow.</summary>
public partial class SetupWorkflowDetailViewModel : ObservableObject, IDisposable
{
    /// <summary>Initializes a workflow host.</summary>
    /// <param name="descriptor">The workflow definition.</param>
    public SetupWorkflowDetailViewModel(SetupWorkflowDescriptor descriptor) => Descriptor = descriptor;

    /// <summary>Gets the workflow definition.</summary>
    public SetupWorkflowDescriptor Descriptor { get; }

    /// <summary>Gets the workflow title.</summary>
    public string Title => Descriptor.Title;

    /// <summary>Gets the workflow purpose.</summary>
    public string Description => Descriptor.Description;

    /// <summary>Gets whether this workflow links to an existing Config page.</summary>
    public bool HasConfigDestination => Descriptor.ConfigDestination is not null;

    /// <summary>Gets or sets the current operation progress from zero to one.</summary>
    [ObservableProperty]
    public partial double Progress { get; set; }

    /// <summary>Gets or sets the latest shared workflow error.</summary>
    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>Cancels work owned by this workflow host.</summary>
    public virtual void Cancel()
    {
    }

    /// <inheritdoc />
    public virtual void Dispose() => Cancel();
}
