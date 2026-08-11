using MissionPlanner.App.Navigation;

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
    }

    /// <inheritdoc/>
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        ItemListView.BindingContext = ViewModel!.Map;
    }

    /// <inheritdoc />
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        ItemListView.BindingContext = null;
    }
}
