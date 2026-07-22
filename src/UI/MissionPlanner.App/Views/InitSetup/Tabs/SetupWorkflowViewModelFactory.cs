using MissionPlanner.Core.Setup;

namespace MissionPlanner.App.Views.InitSetup.Tabs;

/// <summary>Creates generic or specialized workflow hosts only when selected.</summary>
public sealed class SetupWorkflowViewModelFactory : ISetupWorkflowViewModelFactory
{
    private readonly IServiceProvider services;

    /// <summary>Initializes the lazy workflow factory.</summary>
    /// <param name="services">The application service provider.</param>
    public SetupWorkflowViewModelFactory(IServiceProvider services)
    {
        this.services = services;
    }

    /// <inheritdoc />
    public SetupWorkflowDetailViewModel Create(SetupWorkflowDescriptor descriptor)
    {
        return descriptor.Key switch
        {
            SetupWorkflowKey.Firmware => ActivatorUtilities.CreateInstance<FirmwareSetupViewModel>(services, descriptor),
            SetupWorkflowKey.Frame => ActivatorUtilities.CreateInstance<FrameSetupViewModel>(services, descriptor),
            SetupWorkflowKey.Accelerometer => ActivatorUtilities.CreateInstance<AccelerometerSetupViewModel>(services, descriptor),
            SetupWorkflowKey.Compass => ActivatorUtilities.CreateInstance<CompassSetupViewModel>(services, descriptor),
            SetupWorkflowKey.Radio => ActivatorUtilities.CreateInstance<RadioSetupViewModel>(services, descriptor),
            SetupWorkflowKey.FlightModes => ActivatorUtilities.CreateInstance<FlightModesSetupViewModel>(services, descriptor),
            SetupWorkflowKey.Battery => ActivatorUtilities.CreateInstance<BatterySetupViewModel>(services, descriptor),
            SetupWorkflowKey.Esc => ActivatorUtilities.CreateInstance<EscMotorSetupViewModel>(services, descriptor),
            SetupWorkflowKey.ServoOutput => ActivatorUtilities.CreateInstance<ServoOutputSetupViewModel>(services, descriptor),
            SetupWorkflowKey.OptionalHardware => ActivatorUtilities.CreateInstance<OptionalHardwareSetupViewModel>(services, descriptor),
            SetupWorkflowKey.Safety => ActivatorUtilities.CreateInstance<SafetySetupViewModel>(services, descriptor),
            SetupWorkflowKey.Summary => ActivatorUtilities.CreateInstance<SetupSummaryViewModel>(services, descriptor),
            var _ => new SetupWorkflowDetailViewModel(descriptor)
        };
    }
}
