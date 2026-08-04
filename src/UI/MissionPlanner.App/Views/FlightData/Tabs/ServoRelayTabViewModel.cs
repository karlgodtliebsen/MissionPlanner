using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class ServoRelayTabViewModel(IActiveVehicleContext activeVehicle)
    : FlightDataTabViewModelBase("Servo/Relay", activeVehicle);
