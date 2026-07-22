using MissionPlanner.Core.Setup;
using AccelerometerSetupViewModel = MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections.AccelerometerSetupViewModel;
using BatterySetupViewModel = MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections.BatterySetupViewModel;
using CompassSetupViewModel = MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections.CompassSetupViewModel;
using EscMotorSetupViewModel = MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections.EscMotorSetupViewModel;
using FirmwareSetupViewModel = MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections.FirmwareSetupViewModel;
using FlightModesSetupViewModel = MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections.FlightModesSetupViewModel;
using FrameSetupViewModel = MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections.FrameSetupViewModel;
using OptionalHardwareSetupViewModel = MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections.OptionalHardwareSetupViewModel;
using RadioSetupViewModel = MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections.RadioSetupViewModel;
using SafetySetupViewModel = MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections.SafetySetupViewModel;
using ServoOutputSetupViewModel = MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections.ServoOutputSetupViewModel;
using SetupSummaryViewModel = MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections.SetupSummaryViewModel;
using SetupWorkflowDetailViewModel = MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections.SetupWorkflowDetailViewModel;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Services;

/// <summary>
/// Creates generic or specialized workflow hosts only when selected.
/// </summary>
public sealed class SetupWorkflowViewModelFactory : ISetupWorkflowViewModelFactory
{
    private readonly IServiceProvider services;

    /// <summary>
    /// Initializes the lazy workflow factory.
    /// </summary>
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
