using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Views.Navigation;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Links Optional Hardware to the authoritative Onboard OSD configuration workspace.</summary>
public sealed partial class OnboardOsdBridgeViewModel(INavigationService navigation, ILogger<OnboardOsdBridgeViewModel> logger) : ViewModelBase(logger)
{
    /// <summary>Opens the existing OSD editor.</summary>
    [RelayCommand]
    private Task OpenOnboardOsdAsync()
    {
        //TODO: return navigation.OpenSubViewAsync("Config", "Onboard OSD");
        throw new NotImplementedException();
    }
}

