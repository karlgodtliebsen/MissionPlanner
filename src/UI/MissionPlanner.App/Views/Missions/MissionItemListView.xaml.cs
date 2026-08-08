using MissionPlanner.App.Navigation;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Two-mode waypoint list/editor.
/// Mode 1 (default) is the compact list;
/// Mode 2 ("Complete")b mirrors the classic MissionPlanner waypoint grid with editable params/coordinates,
/// derived leg columns (Dist/AZ/Grad) and a header with mission info and editor settings.
/// </summary>
public partial class MissionItemListView : ExtendedContentView<MissionItemListViewModel>
{
    /// <summary>
    /// Gets or sets a value indicating whether all rows are displayed.
    /// </summary>
    public bool ShowAllRows
    {
        get => (bool)GetValue(ShowAllRowsProperty);
        set => SetValue(ShowAllRowsProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="ShowAllRows"/> bindable property.
    /// </summary>
    public static readonly BindableProperty ShowAllRowsProperty = BindableProperty.Create(nameof(ShowAllRows), typeof(bool), typeof(MissionItemListView), true);

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionItemListView"/> class.
    /// </summary>
    public MissionItemListView()
    {
        InitializeComponent();
    }
}
