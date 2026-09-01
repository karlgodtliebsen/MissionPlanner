using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public partial class MainShellViewModel : ObservableObject
{
    private readonly INavigationService navigationService;

    public MainShellViewModel(INavigationService navigationService)
    {
        this.navigationService = navigationService;
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
        MissionPlannerRoutes.FlightData or
        MissionPlannerRoutes.FlightPlanner or
        MissionPlannerRoutes.Preferences or
        MissionPlannerRoutes.SetupInstallFirmware or
        MissionPlannerRoutes.SetupMandatoryHardware or
        MissionPlannerRoutes.Simulation or
        MissionPlannerRoutes.Introduction or
        MissionPlannerRoutes.Help or
        MissionPlannerRoutes.Exit;
    }
}
