using MissionPlanner.App.Navigation;

namespace MissionPlanner.App.Views.Missions.DockView;

/// <summary>
/// Represents the view for the mission item list dock.
/// </summary>
public partial class MissionItemListDockView : ExtendedContentView<MissionItemListDockViewModel>
{
    /// <summary>
    /// Occurs when the width request changes.
    /// </summary>
    public event EventHandler<WidthEventArgs>? WidthRequestChanged;

    private double ShrinkWidth { get; set; } = 50;
    private double ExpandWidth { get; set; } = 700;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionItemListDockView"/> class.
    /// </summary>
    public MissionItemListDockView()
    {
        InitializeComponent();
        ViewModel!.ShrinkWidth = ShrinkWidth;
        ViewModel!.ExpandWidth = ExpandWidth;
        ViewModel!.WidthRequestChanged += ViewModel_WidthRequestChanged;
    }

    private void ViewModel_WidthRequestChanged(object? sender, WidthEventArgs e)
    {
        WidthRequestChanged?.Invoke(this, e);
    }
}
