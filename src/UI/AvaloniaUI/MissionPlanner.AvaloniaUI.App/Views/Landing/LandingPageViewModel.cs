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
    private async Task OpenFlightDataAsync()
    {
        //TODO:  await navigation.OpenPageAsync("Flight Data");
        throw new NotImplementedException();

    }

    [RelayCommand]
    private async Task OpenTutorialAsync()
    {
        //TODO: await navigation.OpenPageAsync("Tutorial");
        throw new NotImplementedException();

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

