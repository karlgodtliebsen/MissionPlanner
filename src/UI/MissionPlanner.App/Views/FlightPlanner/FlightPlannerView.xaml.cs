using MissionPlanner.App.Navigation;
using MissionPlanner.App.Views.Missions.DockView;

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
        TheGridSplitter.DragStarted += TheGridSplitter_DragStarted;
        TheGridSplitter.DragRunning += TheGridSplitter_DragRunning;
        TheGridSplitter.DraggedCompleted += TheGridSplitter_DraggedCompleted;
    }

    private void TheGridSplitter_DragStarted(object? sender, EventArgs e)
    {
        Editor.IsVisible = false;
    }

    private void TheGridSplitter_DragRunning(object? sender, EventArgs e)
    {
    }

    private void TheGridSplitter_DraggedCompleted(object? sender, EventArgs e)
    {
        var width = TheGrid.ColumnDefinitions[2].Width.Value;
        if (Math.Abs(width - Editor.MinimumWidthRequest) > 0.1)
        {
            Editor.WidthRequest = width;
        }

        Editor.IsVisible = true;
    }

    private void Editor_WidthRequestChanged(object? sender, WidthEventArgs e)
    {
        var width = e.Width;
        if (Math.Abs(width - Editor.MinimumWidthRequest) > 0.1)
        {
            Editor.WidthRequest = width;
            TheGrid.ColumnDefinitions[2].Width = new GridLength(width);
            ViewModel!.IsExpanded = e.IsExpanded;
        }
    }
}
