using MissionPlanner.App.Navigation;
using MissionPlanner.App.Views.Missions;

namespace MissionPlanner.App.Views.FlightPlanner;

/// <summary>
/// Represents the view for flight planning.
/// </summary>
public partial class FlightPlannerView : ExtendedContentPage<FlightPlannerViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlightPlannerView"/> class.
    /// </summary>
    public FlightPlannerView()
    {
        InitializeComponent();
        Editor.WidthRequestChanged += Editor_WidthRequestChanged;
        //TheGridSplitter.On
    }

    private void Editor_WidthRequestChanged(object? sender, WidthEventArgs e)
    {
        //Editor.MinimumWidthRequest = e.Width;
        TheGrid.ColumnDefinitions[2].Width = new GridLength(e.Width);
    }
}
