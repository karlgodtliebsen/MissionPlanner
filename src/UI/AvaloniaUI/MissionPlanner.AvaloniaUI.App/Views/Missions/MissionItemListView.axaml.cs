using MissionPlanner.AvaloniaUI.App.Utilities;

namespace MissionPlanner.AvaloniaUI.App.Views.Missions;

/// <summary>
/// Two-mode waypoint list/editor.
/// Mode 1 (default) is the compact list;
/// Mode 2 ("Complete")b mirrors the classic MissionPlanner waypoint grid with editable params/coordinates,
/// derived leg columns (Dist/AZ/Grad) and a header with mission info and editor settings.
/// </summary>
public partial class MissionItemListView : ViewBase
{
    //[ObservableProperty]
    //public partial string ShowAllRows
    //{
    //    get; set;
    //}

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionItemListView"/> class.
    /// </summary>
    public MissionItemListView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 
    /// </summary>
    public bool ShowAllRows
    {
        get;
    } = true;
}
