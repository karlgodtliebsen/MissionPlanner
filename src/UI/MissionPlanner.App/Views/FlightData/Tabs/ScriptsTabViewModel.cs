using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class ScriptsTabViewModel(IActiveVehicleContext activeVehicle)
    : FlightDataTabViewModelBase("Scripts", activeVehicle);
