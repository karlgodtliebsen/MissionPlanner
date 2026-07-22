using MissionPlanner.Core.Setup;

namespace MissionPlanner.App.Views.InitSetup;

/// <summary>Creates the default workflow hosts used by the Setup shell.</summary>
public sealed class SetupWorkflowViewModelFactory : ISetupWorkflowViewModelFactory
{
    /// <inheritdoc />
    public SetupWorkflowDetailViewModel Create(SetupWorkflowDescriptor descriptor) => new(descriptor);
}
