using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class PreflightTabViewModel(IActiveVehicleContext activeVehicle)
    : FlightDataTabViewModelBase("PreFlight", activeVehicle);
