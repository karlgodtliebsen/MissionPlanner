using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed class RangefinderViewModel(IActiveVehicleContext v, IOptionalHardwareService s, ILogger<RangefinderViewModel> logger, INavigationService navigation) : ParameterHardwareViewModel("rangefinder", v, s, logger, navigation);

