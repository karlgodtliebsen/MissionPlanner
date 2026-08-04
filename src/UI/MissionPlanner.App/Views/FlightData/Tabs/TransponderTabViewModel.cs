using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class TransponderTabViewModel(IActiveVehicleContext activeVehicle)
    : FlightDataTabViewModelBase("Transponder", activeVehicle);
