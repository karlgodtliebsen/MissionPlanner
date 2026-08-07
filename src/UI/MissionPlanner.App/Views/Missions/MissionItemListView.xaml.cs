using UraniumUI.Pages;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Two-mode waypoint list/editor.
/// Mode 1 (default) is the compact list;
/// Mode 2 ("Complete")b mirrors the classic MissionPlanner waypoint grid with editable params/coordinates,
/// derived leg columns (Dist/AZ/Grad) and a header with mission info and editor settings.
/// Bound to the singleton <see cref="MissionItemListViewModel"/>.
/// </summary>
public partial class MissionItemListView : UraniumContentPage // ExtendedContentPage<MissionItemListViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MissionItemListView"/> class.
    /// </summary>
    public MissionItemListView(MissionItemListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
