namespace MissionPlanner.App.Views.FlightData.Tabs;

public partial class ActionsTabView : ContentView
{
    public ActionsTabView(ActionsTabViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}