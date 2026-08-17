using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Navigation;

namespace MissionPlanner.App.Views.Landing;

/// <summary>
/// ViewModel for the LandingPage.
/// </summary>
public partial class LandingPageViewModel(INavigationService navigation) : ObservableObject, IDisposable
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
    public void Dispose()
    {
    }
}
