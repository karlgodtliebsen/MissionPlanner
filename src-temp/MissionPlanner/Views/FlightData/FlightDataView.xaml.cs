using MissionPlanner.Views.FlightData.Hud;
using MissionPlanner.Views.FlightData.Map;
using MissionPlanner.Views.FlightData.Tabs;

namespace MissionPlanner.Views.FlightData;

public partial class FlightDataView : ContentView
{
    private readonly QuickTabView quickTabView;
    private readonly ActionsTabView actionsTabView;
    private readonly StatusTabView statusTabView;

    private static readonly Color SelectedTabColor = Color.FromArgb("#3e3e42");
    private static readonly Color SelectedTabBorderColor = Colors.DodgerBlue;
    private static readonly Color UnselectedTabColor = Color.FromArgb("#2d2d30");

    private Button? selectedTab;

    public FlightDataView(
        FlightDataViewModel viewModel,
        HudView hudView,
        FlightDataMapView flightDataMapView,
        QuickTabView quickTabView,
        ActionsTabView actionsTabView,
        StatusTabView statusTabView)
    {
        InitializeComponent();
        BindingContext = viewModel;

        this.quickTabView = quickTabView;
        this.actionsTabView = actionsTabView;
        this.statusTabView = statusTabView;

        HudHost.Content = hudView;
        MapHost.Content = flightDataMapView;

        SelectTab(QuickTabButton, quickTabView);
    }

    private void QuickTabButton_Clicked(object? sender, EventArgs e)
    {
        SelectTab(QuickTabButton, quickTabView);
    }

    private void ActionsTabButton_Clicked(object? sender, EventArgs e)
    {
        SelectTab(ActionsTabButton, actionsTabView);
    }

    private void StatusTabButton_Clicked(object? sender, EventArgs e)
    {
        SelectTab(StatusTabButton, statusTabView);
    }

    private void SelectTab(Button tab, ContentView content)
    {
        if (selectedTab == tab)
        {
            return;
        }

        if (selectedTab is not null)
        {
            selectedTab.BackgroundColor = UnselectedTabColor;
            selectedTab.BorderColor = Colors.Transparent;
        }

        tab.BackgroundColor = SelectedTabColor;
        tab.BorderColor = SelectedTabBorderColor;
        selectedTab = tab;

        ActionTabContent.Content = content;
    }
}