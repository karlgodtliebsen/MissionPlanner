using Avalonia;
using MissionPlanner.AvaloniaUI.App.Utilities;

namespace MissionPlanner.AvaloniaUI.App.Views.Missions;

/// <summary>
/// Two-mode waypoint list/editor.
/// Mode 1 (default) is the compact list;
/// Mode 2 ("Complete")b mirrors the classic MissionPlanner waypoint grid with editable params/coordinates,
/// derived leg columns (Dist/AZ/Grad) and a header with mission info and editor settings.
/// </summary>
public partial class MissionItemListView : UserControlViewBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MissionItemListView"/> class.
    /// </summary>
    public MissionItemListView()
    {
        InitializeComponent();
        ShowAllRows = false;
    }

    /// <summary>
    /// 
    /// </summary>
    public bool ShowAllRows
    {
        get => GetValue(ShowAllRowsProperty); set => SetValue(ShowAllRowsProperty, value);
    }

    /// <summary>Controls whether the complete editor columns are displayed.</summary>
    public static readonly StyledProperty<bool> ShowAllRowsProperty = AvaloniaProperty.Register<MissionItemListView, bool>(nameof(ShowAllRows), true);
}
