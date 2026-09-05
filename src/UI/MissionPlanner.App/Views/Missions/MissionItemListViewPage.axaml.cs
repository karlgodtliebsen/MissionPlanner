namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Two-mode waypoint list/editor.
/// Mode 1 (default) is the compact list;
/// Mode 2 ("Complete")b mirrors the classic MissionPlanner waypoint grid with editable params/coordinates,
/// derived leg columns (Dist/AZ/Grad) and a header with mission info and editor settings.
/// Bound to the keyed singleton <see cref="MissionMapViewModel"/> provided by host.
/// </summary>
public partial class MissionItemListViewPage : UserControlViewBase
{
    /// <summary>Initializes a new instance for dialog hosting.</summary>
    public MissionItemListViewPage()
    {
        InitializeComponent();
    }
}
