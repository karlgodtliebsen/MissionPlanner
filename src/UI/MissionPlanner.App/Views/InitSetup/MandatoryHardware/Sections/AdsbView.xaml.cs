using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Displays supported ADS-B and avoidance settings.</summary>
public partial class AdsbView : TabViewLifecycleContent<AdsbViewModel>
{
    /// <summary>Initializes a new ADS-B view.</summary>
    public AdsbView()
    {
        InitializeComponent();
    }
}
