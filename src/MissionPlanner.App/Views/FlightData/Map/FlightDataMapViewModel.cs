using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.App.Views.FlightData.Map;

public partial class FlightDataMapViewModel : ObservableObject
{
    [ObservableProperty] public partial double VehicleLatitude { get; set; }

    [ObservableProperty] public partial double VehicleLongitude { get; set; }

    [ObservableProperty] public partial double VehicleHeading { get; set; }
}