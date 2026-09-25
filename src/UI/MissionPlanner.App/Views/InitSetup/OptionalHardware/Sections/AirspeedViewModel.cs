using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>
/// ViewModel for configuring the airspeed optional hardware. 
/// </summary>
/// <param name="v"></param>
/// <param name="s"></param>
/// <param name="logger"></param>
/// <param name="navigation">The application navigation service.</param>
public sealed class AirspeedViewModel(IActiveVehicleContext v, IOptionalHardwareService s, ILogger<AirspeedViewModel> logger, INavigationService navigation)
    : ParameterHardwareViewModel("airspeed", v, s, logger, navigation);

