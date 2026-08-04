using MissionPlanner.App.Navigation;
using UraniumUI.Extensions;
using UraniumUI.Material.Controls;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>
/// Represents the view for displaying flight data.
/// </summary>
public partial class FlightDataView : ContentPageView<FlightDataViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlightDataView"/> class.
    /// </summary>
    public FlightDataView()
    {
        InitializeComponent();
        TabView.SelectedTabChanged += (sender, item) => SelectTabSet(item).FireAndForget();
    }

    private async Task SelectTabSet(TabItem tabItem)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.SelectTabAsync(TabView.Tabs.IndexOf(tabItem));
    }
}
