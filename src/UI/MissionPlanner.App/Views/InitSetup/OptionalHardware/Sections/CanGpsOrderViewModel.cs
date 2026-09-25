using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed class CanGpsOrderViewModel(IActiveVehicleContext v, IOptionalHardwareService s, ILogger<CanGpsOrderViewModel> logger, INavigationService navigation) : ParameterHardwareViewModel("can-gps-order", v, s, logger, navigation);



