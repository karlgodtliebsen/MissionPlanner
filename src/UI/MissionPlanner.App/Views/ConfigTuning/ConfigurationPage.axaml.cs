using Avalonia.Controls;
using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.ConfigTuning;

/// <summary>The Configuration page: tuning, geofence, OSD and file tools in tabs.</summary>
public partial class ConfigurationPage : NavigationViewBase<ConfigurationViewModel>, ISectionNavigationPage
{
    /// <summary>Creates the page.</summary>
    public ConfigurationPage() => InitializeComponent();

    /// <summary>Selects the configuration tab with the given name.</summary>
    public void SelectSection(string section)
    {
        ConfigurationTabs.SelectedItem = ConfigurationTabs.Items.OfType<TabItem>()
            .FirstOrDefault(tab => tab.Name == section)
            ?? throw new ArgumentException($"Unknown Configuration section '{section}'.", nameof(section));
    }
}
