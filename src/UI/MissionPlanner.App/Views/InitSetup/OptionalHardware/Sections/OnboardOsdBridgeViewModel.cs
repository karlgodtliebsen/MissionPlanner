using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Navigation;
using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Links Optional Hardware to the authoritative Onboard OSD configuration workspace.</summary>
public sealed partial class OnboardOsdBridgeViewModel(INavigationService navigation, ILogger<OnboardOsdBridgeViewModel> logger) : BaseViewModel(logger)
{
    /// <summary>Opens the existing OSD editor.</summary>
    [RelayCommand]
    private Task OpenOnboardOsdAsync()
    {
        return navigation.OpenSubViewAsync("Config", "Onboard OSD");
    }
}
