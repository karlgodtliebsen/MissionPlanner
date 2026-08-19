using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed class OpticalFlowViewModel(IActiveVehicleContext v, IOptionalHardwareService s) : ParameterHardwareViewModel("optical-flow", v, s)
{
    /// <summary>Gets the focus/image capability status.</summary>
    public string FocusCapabilityStatus => "PX4Flow focus imagery requires a compatible image handshake stream. Focus mode remains unavailable until that stream is detected; parameter configuration is independent.";
}
