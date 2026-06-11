namespace MissionPlanner.Views.MenuFlightData;

public partial class MenuFlightDataView : ContentView
{
    public MenuFlightDataView(MenuFlightDataViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
