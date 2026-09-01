using AsyncAwaitBestPractices;
using Avalonia.Interactivity;
using MissionPlanner.AvaloniaUI.App.Utilities;

namespace MissionPlanner.AvaloniaUI.App.Views.FlightData;

/// <summary>
/// Interaction logic for FlightDataPage.xaml
/// </summary>
public partial class FlightDataPage : NavigationViewBase<FlightDataViewModel>
{

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightDataPage"/> class.
    /// </summary>
    public FlightDataPage()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        MapLoadingIndicator.IsVisible = true;
        ViewModel.Map.ActivateAsync().SafeFireAndForget();
        MapView.ActivateAsync(ViewModel.Map).SafeFireAndForget();
        MapLoadingIndicator.IsVisible = false;

    }

    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        MapView.DeactivateAsync().SafeFireAndForget();
        ViewModel.Map.Deactivate();
        base.OnUnloaded(e);
    }
}
