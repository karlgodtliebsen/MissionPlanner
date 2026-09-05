using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Views.Navigation;

namespace MissionPlanner.AvaloniaUI.App.Views.Landing;

/// <summary>
/// ViewModel for the LandingPage.
/// </summary>
public partial class LandingPageViewModel(INavigationService navigation, ILogger<LandingPageViewModel> logger) : ViewModelBase(logger)
{
    [RelayCommand]
    private Task OpenFlightDataAsync()
    {
        return navigation.NavigateAsync(MissionPlannerRoutes.FlightData);
    }

    [RelayCommand]
    private Task OpenTutorialAsync()
    {
        return navigation.NavigateAsync(MissionPlannerRoutes.Introduction);
    }


    /// <inheritdoc />
    public override void Dispose()
    {
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        return Task.CompletedTask;
    }
}
