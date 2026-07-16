using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Two-mode waypoint list/editor. Mode 1 (default) is the compact list; Mode 2 ("Complete")
/// mirrors the classic MissionPlanner waypoint grid with editable params/coordinates, derived
/// leg columns (Dist/AZ/Grad) and a header with mission info and editor settings.
/// Bound to the singleton <see cref="MissionMapViewModel"/>.
/// </summary>
public partial class MissionItemListView : ContentView
{
    private readonly MissionMapViewModel viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionItemListView"/> class.
    /// </summary>
    public MissionItemListView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<MissionMapViewModel>();
        BindingContext = viewModel;
    }

    private void OnRowEditCompleted(object? sender, EventArgs e)
    {
        if (sender is Entry { BindingContext: MissionItemRow row })
        {
            // Stale rows (already replaced by a rebuild) are ignored by the command.
            viewModel.ApplyRowEditCommand.Execute(row);
        }
    }
}
