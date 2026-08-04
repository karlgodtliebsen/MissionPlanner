using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class GaugesTabViewModel(IActiveVehicleContext activeVehicle)
    : FlightDataTabViewModelBase("Gauges", activeVehicle);
