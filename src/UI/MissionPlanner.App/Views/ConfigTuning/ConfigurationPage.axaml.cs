using Avalonia.Controls;
using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.ConfigTuning;

public partial class ConfigurationPage : NavigationViewBase<ConfigurationViewModel>, ISectionNavigationPage
{
    public ConfigurationPage() => InitializeComponent();

    public void SelectSection(string section)
    {
        ConfigurationTabs.SelectedItem = ConfigurationTabs.Items.OfType<TabItem>()
            .FirstOrDefault(tab => tab.Name == section)
            ?? throw new ArgumentException($"Unknown Configuration section '{section}'.", nameof(section));
    }
}
