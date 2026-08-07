using MissionPlanner.App.Navigation;

namespace MissionPlanner.App.Views.Missions.DockView;

/// <summary>
/// Represents the view for the mission item list dock.
/// </summary>
public partial class MissionItemListDockView : ExtendedContentView<MissionItemListDockViewModel>, IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MissionItemListDockViewModel"/> class.
    /// </summary>
    public MissionItemListDockView()
    {
        InitializeComponent();
    }


    /// <inheritdoc />
    public override void Dispose()
    {
        // base.Dispose();
    }
}
