using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Navigation;
using UraniumUI.Material.TabViews;

namespace MissionPlanner.App.Views.Landing;

/// <summary>
/// ViewModel for the LandingPage.
/// </summary>
public partial class LandingPageViewModel(INavigationService navigation, ILogger<LandingPageViewModel> logger) : BaseViewModel(logger)
{
    [RelayCommand]
    private async Task OpenFlightDataAsync()
    {
        await navigation.OpenPageAsync("Flight Data");
    }

    [RelayCommand]
    private async Task OpenTutorialAsync()
    {
        await navigation.OpenPageAsync("Tutorial");
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
