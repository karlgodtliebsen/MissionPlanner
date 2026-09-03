using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public partial class MainShellViewModel : ObservableObject
{
    private readonly INavigationService navigationService;
    private readonly IWindowProvider windowProvider;

    public MainShellViewModel(
        INavigationService navigationService,
        IWindowProvider windowProvider)
    {
        this.navigationService = navigationService;
        this.windowProvider = windowProvider;
    }

    [ObservableProperty]
    public partial bool IsNavigationOpen
    {
        get;
        set;
    }

    [RelayCommand]
    public void ToggleNavigation()
    {
        IsNavigationOpen = !IsNavigationOpen;
    }

    [RelayCommand]
    private void Exit()
    {
        windowProvider.ActiveWindow?.Close();
    }

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    public async Task NavigateAsync(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return;
        }

        await navigationService.NavigateAsync(route);
    }

    private static bool CanNavigate(string? route)
    {
        return route is
        //MissionPlannerRoutes.DataGridDemo or
        //MissionPlannerRoutes.DialogDemo or
        MissionPlannerRoutes.FlightData or
        MissionPlannerRoutes.FlightPlanner or
        MissionPlannerRoutes.Preferences or
        MissionPlannerRoutes.SetupInstallFirmware or
        MissionPlannerRoutes.SetupMandatoryHardware or
        MissionPlannerRoutes.SetupOptionalHardware or
        MissionPlannerRoutes.SetupAdvanced or


        MissionPlannerRoutes.ConfigOnboardOSD or
        MissionPlannerRoutes.ConfigFullParameters or
        MissionPlannerRoutes.ConfigBasicTuning or
        MissionPlannerRoutes.ConfigGeoFence or
        MissionPlannerRoutes.ConfigExtendedTuning or
        MissionPlannerRoutes.ConfigCubeLan8PortSwitch or
        MissionPlannerRoutes.ConfigMavFtp or
        MissionPlannerRoutes.ConfigOnboardOSD or
        MissionPlannerRoutes.Simulation or
        MissionPlannerRoutes.Introduction or
        MissionPlannerRoutes.Help;
    }
}
