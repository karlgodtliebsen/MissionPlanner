using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class AuxFunctionTabViewModel(IActiveVehicleContext activeVehicle)
    : FlightDataTabViewModelBase("Aux Function", activeVehicle);
