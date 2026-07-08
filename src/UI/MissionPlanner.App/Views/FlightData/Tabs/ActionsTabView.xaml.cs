namespace MissionPlanner.App.Views.FlightData.Tabs;

public partial class ActionsTabView : ContentView
{
    public ActionsTabView(ConfigTuning.Tabs.FullParametersListTabViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
