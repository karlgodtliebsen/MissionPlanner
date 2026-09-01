using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.OptionalHardware.Sections;

public sealed class RangefinderViewModel(IActiveVehicleContext v, IOptionalHardwareService s, ILogger<RangefinderViewModel> logger) : ParameterHardwareViewModel("rangefinder", v, s, logger);

