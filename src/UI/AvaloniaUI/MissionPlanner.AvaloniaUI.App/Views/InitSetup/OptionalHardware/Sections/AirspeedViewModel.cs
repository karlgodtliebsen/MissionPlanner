using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>
/// ViewModel for configuring the airspeed optional hardware. 
/// </summary>
/// <param name="v"></param>
/// <param name="s"></param>
/// <param name="logger"></param>
public sealed class AirspeedViewModel(IActiveVehicleContext v, IOptionalHardwareService s, ILogger<AirspeedViewModel> logger)
    : ParameterHardwareViewModel("airspeed", v, s, logger);

