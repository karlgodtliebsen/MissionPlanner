using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed class ParachuteViewModel(IActiveVehicleContext v, IOptionalHardwareService s) : ParameterHardwareViewModel("parachute", v, s);
