using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Provides the Status tab presentation lifecycle.</summary>
public partial class StatusTabViewModel(IActiveVehicleContext activeVehicle)
    : FlightDataTabViewModelBase("Status", activeVehicle);
