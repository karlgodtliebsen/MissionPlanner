using MissionPlanner.AvaloniaUI.App.Utilities;

namespace MissionPlanner.AvaloniaUI.App.Views.FlightData;

public partial class FlightDataPage : NavigationViewBase<FlightDataViewModel>
{

    public FlightDataPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        MapLoadingIndicator.IsVisible = true;
        await ViewModel.Map.ActivateAsync();
        await MapView.ActivateAsync(ViewModel.Map);
        MapLoadingIndicator.IsVisible = false;
    }

    private async void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        await MapView.DeactivateAsync();
        ViewModel.Map.Deactivate();
    }
}
