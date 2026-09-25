using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>
///  
/// </summary>
/// <param name="v"></param>
/// <param name="s"></param>
/// <param name="logger"></param>
/// <param name="navigation">The application navigation service.</param>
public sealed class OpticalFlowViewModel(IActiveVehicleContext v, IOptionalHardwareService s, ILogger<OpticalFlowViewModel> logger, INavigationService navigation) :

    ParameterHardwareViewModel("optical-flow", v, s, logger, navigation)
{
    /// <summary>Gets the focus/image capability status.</summary>
    public string FocusCapabilityStatus => "PX4Flow focus imagery requires a compatible image handshake stream. Focus mode remains unavailable until that stream is detected; parameter configuration is independent.";
}

