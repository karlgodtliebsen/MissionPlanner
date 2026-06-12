namespace MissionPlanner.Views.FlightData.Map;

public partial class FlightDataMapView : ContentView
{
    public FlightDataMapView(FlightDataMapViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}