using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed class CanGpsOrderViewModel(IActiveVehicleContext v, IOptionalHardwareService s, ILogger<CanGpsOrderViewModel> logger) : ParameterHardwareViewModel("can-gps-order", v, s, logger);


