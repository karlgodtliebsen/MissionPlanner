using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed class ParachuteViewModel(IActiveVehicleContext v, IOptionalHardwareService s, ILogger<ParachuteViewModel> logger) : ParameterHardwareViewModel("parachute", v, s, logger);
