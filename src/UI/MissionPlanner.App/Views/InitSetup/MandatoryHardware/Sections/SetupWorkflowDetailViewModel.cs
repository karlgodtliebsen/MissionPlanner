using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.Setup;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Provides the common presentation and lifecycle state for one Setup workflow.</summary>
public partial class SetupWorkflowDetailViewModel : ObservableObject, IDisposable
{
    private bool active;
    private bool disposed;

    /// <summary>Initializes a workflow ViewModel.</summary>
    /// <param name="descriptor">The workflow definition.</param>
    public SetupWorkflowDetailViewModel(SetupWorkflowDescriptor descriptor)
    {
        Descriptor = descriptor;
    }

    /// <summary>Gets the workflow definition.</summary>
    public SetupWorkflowDescriptor Descriptor { get; }

    /// <summary>Gets the workflow title.</summary>
    public string Title => Descriptor.Title;

    /// <summary>Gets the workflow purpose.</summary>
    public string Description => Descriptor.Description;

    /// <summary>Gets whether this workflow links to an existing Config page.</summary>
    public bool HasConfigDestination => Descriptor.ConfigDestination is not null;

    /// <summary>Gets whether this section currently owns its active UI lifecycle.</summary>
    public bool IsActive => active;

    /// <summary>Gets or sets the current operation progress from zero to one.</summary>
    [ObservableProperty]
    public partial double Progress { get; set; }

    /// <summary>Gets or sets the latest workflow error.</summary>
    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>Activates the section when its owning view becomes visible.</summary>
    public void Activate()
    {
        if (active || disposed)
        {
            return;
        }

        active = true;
        OnActivated();
    }

    /// <summary>Deactivates the section when its owning view is hidden or leaves the visual tree.</summary>
    public void Deactivate()
    {
        if (!active)
        {
            return;
        }

        active = false;
        OnDeactivated();
    }

    /// <summary>Cancels work owned by this workflow ViewModel.</summary>
    public virtual void Cancel()
    {
    }

    /// <summary>Handles section-specific activation.</summary>
    protected virtual void OnActivated()
    {
    }

    /// <summary>Handles section-specific deactivation.</summary>
    protected virtual void OnDeactivated()
    {
        Cancel();
    }

    /// <inheritdoc />
    public virtual void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (active)
        {
            Deactivate();
        }
        else
        {
            Cancel();
        }
    }
}
