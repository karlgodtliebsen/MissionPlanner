using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.Core.Setup;

namespace MissionPlanner.App.Views.InitSetup;

/// <summary>Creates generic or specialized workflow hosts only when selected.</summary>
public sealed class SetupWorkflowViewModelFactory : ISetupWorkflowViewModelFactory
{
    private readonly IServiceProvider services;

    /// <summary>Initializes the lazy workflow factory.</summary>
    /// <param name="services">The application service provider.</param>
    public SetupWorkflowViewModelFactory(IServiceProvider services) => this.services = services;

    /// <inheritdoc />
    public SetupWorkflowDetailViewModel Create(SetupWorkflowDescriptor descriptor) => descriptor.Key switch
    {
        SetupWorkflowKey.Firmware => ActivatorUtilities.CreateInstance<FirmwareSetupViewModel>(services, descriptor),
        SetupWorkflowKey.Frame => ActivatorUtilities.CreateInstance<FrameSetupViewModel>(services, descriptor),
        _ => new SetupWorkflowDetailViewModel(descriptor)
    };
}
