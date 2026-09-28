using MissionPlanner.App.Utilities;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware;

/// <summary>Hosts all optional hardware setup workflows.</summary>
public partial class OptionalHardwarePage : NavigationViewBase<OptionalHardwareViewModel>, MissionPlanner.App.Views.Navigation.ISectionNavigationPage
{
    /// <summary>Initializes the optional hardware page.</summary>
    public OptionalHardwarePage()
    {
        InitializeComponent();
    }

    /// <summary>Selects an explicitly addressed hardware section without applying settings.</summary>
    public void SelectSection(string section)
    {
        if (section != "BatteryMonitors")
        {
            throw new ArgumentException($"Unknown optional hardware section: {section}", nameof(section));
        }
        HardwareSections.SelectedItem = BatteryMonitorsSection;
    }
}
