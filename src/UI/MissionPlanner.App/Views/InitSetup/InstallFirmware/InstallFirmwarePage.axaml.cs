using Avalonia.Controls;
using Avalonia.Interactivity;
using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Displays firmware discovery, validation, and installation workflows.</summary>
public partial class InstallFirmwarePage : NavigationViewBase<InstallFirmwareViewModel>
{
    /// <summary>Initializes the firmware page.</summary>
    public InstallFirmwarePage()
    {
        InitializeComponent();
    }

    private async void OpenFirmwareHelp(object? sender, RoutedEventArgs e)
    {
        await ServiceHelper.GetRequiredService<INavigationService>()
            .NavigateAsync(MissionPlannerRoutes.HelpInstallFirmware);
    }


}
