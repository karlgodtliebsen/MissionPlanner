using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed class RangefinderViewModel(IActiveVehicleContext v, IOptionalHardwareService s) : ParameterHardwareViewModel("rangefinder", v, s);
