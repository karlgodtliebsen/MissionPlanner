using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class DataFlashLogsTabViewModel(IActiveVehicleContext activeVehicle)
    : FlightDataTabViewModelBase("DataFlash Logs", activeVehicle);
