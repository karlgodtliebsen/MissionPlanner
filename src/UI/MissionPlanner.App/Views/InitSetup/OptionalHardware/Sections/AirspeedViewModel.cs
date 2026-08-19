using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

public sealed class AirspeedViewModel(IActiveVehicleContext v, IOptionalHardwareService s) : ParameterHardwareViewModel("airspeed", v, s);
