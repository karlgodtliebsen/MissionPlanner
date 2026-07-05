namespace MissionPlanner.App.Views.FlightData.Tabs;

public partial class QuickTabView : ContentView
{
    public QuickTabView(QuickTabViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}