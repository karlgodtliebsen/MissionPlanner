using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Links Optional Hardware to the authoritative Onboard OSD configuration workspace.</summary>
public sealed partial class OnboardOsdBridgeViewModel(INavigationService navigation, ILogger<OnboardOsdBridgeViewModel> logger) : ViewModelBase(logger)
{
    /// <summary>Refreshes the current page state without starting a hardware operation.</summary>
    [RelayCommand]
    private void Refresh()
    {
        SetMessages("Open Onboard OSD to load and configure the connected vehicle’s supported OSD settings.");
    }

    /// <summary>Opens the shared Parameters Editor workspace when no operation is running.</summary>
    [RelayCommand]
    private async Task OpenFullParametersAsync()
    {
        if (IsBusy)
        {
            return;
        }
        await navigation.NavigateAsync(MissionPlannerRoutes.Configuration);
    }

    /// <summary>Opens the existing OSD editor.</summary>
    [RelayCommand]
    private Task OpenOnboardOsdAsync()
    {
        return navigation.NavigateAsync(MissionPlannerRoutes.Configuration);
    }
}
