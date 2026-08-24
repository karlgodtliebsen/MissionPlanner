using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup;
using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models;

/// <summary>Provides the common presentation and lifecycle state for one Setup workflow.</summary>
public partial class SetupWorkflowDetailViewModel : BaseViewModel
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

    /// <summary>Gets or sets the current operation progress from zero to one.</summary>
    [ObservableProperty]
    public partial double Progress
    {
        get;
        set;
    }


    /// <summary>Cancels work owned by this workflow ViewModel.</summary>
    public virtual void Cancel()
    {
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        DeactivateAsync().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        return Task.CompletedTask;
    }
}
