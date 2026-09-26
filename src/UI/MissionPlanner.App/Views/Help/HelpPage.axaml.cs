namespace MissionPlanner.App.Views.Help;

/// <summary>Displays the general Help hub.</summary>
public partial class HelpPage : NavigationViewBase<HelpViewModel>
{
    /// <summary>Creates the Help page and connects its view model.</summary>
    public HelpPage() => InitializeComponent();

    /// <summary>Selects the firmware article for contextual help navigation.</summary>
    public void SelectInstallFirmware() => ViewModel.SelectInstallFirmware();
}
