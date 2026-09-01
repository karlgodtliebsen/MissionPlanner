using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>
///  
/// </summary>
/// <param name="v"></param>
/// <param name="s"></param>
/// <param name="logger"></param>
public sealed class OpticalFlowViewModel(IActiveVehicleContext v, IOptionalHardwareService s, ILogger<OpticalFlowViewModel> logger) :

    ParameterHardwareViewModel("optical-flow", v, s, logger)
{
    /// <summary>Gets the focus/image capability status.</summary>
    public string FocusCapabilityStatus => "PX4Flow focus imagery requires a compatible image handshake stream. Focus mode remains unavailable until that stream is detected; parameter configuration is independent.";
}

