using MissionPlanner.Core.Setup;
using SetupWorkflowDetailViewModel = MissionPlanner.App.Views.InitSetup.Sections.SetupWorkflowDetailViewModel;

namespace MissionPlanner.App.Views.InitSetup.Services;

/// <summary>Creates workflow hosts lazily so later Setup tasks can replace their content independently.</summary>
public interface ISetupWorkflowViewModelFactory
{
    /// <summary>Creates a host for one workflow.</summary>
    /// <param name="descriptor">The workflow definition.</param>
    /// <returns>The newly created workflow host.</returns>
    SetupWorkflowDetailViewModel Create(SetupWorkflowDescriptor descriptor);
}
