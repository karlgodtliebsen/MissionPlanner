namespace MissionPlanner.Views.FlightData.Tabs;

public partial class StatusTabView : ContentView
{
    public StatusTabView(StatusTabViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}