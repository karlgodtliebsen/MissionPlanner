using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed class ParachuteViewModel(IActiveVehicleContext v, IOptionalHardwareService s, ILogger<ParachuteViewModel> logger, INavigationService navigation) : ParameterHardwareViewModel("parachute", v, s, logger, navigation);

