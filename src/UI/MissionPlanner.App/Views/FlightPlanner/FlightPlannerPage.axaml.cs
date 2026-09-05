using AsyncAwaitBestPractices;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.App.Views.FlightPlanner;

/// <summary>Hosts the Flight Planner map, toolbar, and mission editor.</summary>
public partial class FlightPlannerPage : NavigationViewBase<FlightPlannerViewModel>
{
    private CancellationTokenSource? mapActivationCancellation;

    /// <summary>Initializes a new instance of the <see cref="FlightPlannerPage"/> class.</summary>
    public FlightPlannerPage()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        mapActivationCancellation?.Cancel();
        mapActivationCancellation?.Dispose();
        mapActivationCancellation = new CancellationTokenSource();
        ActivateMapAsync(mapActivationCancellation.Token).SafeFireAndForget(OnMapActivationFailed);
    }

    /// <inheritdoc />
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        mapActivationCancellation?.Cancel();
        mapActivationCancellation?.Dispose();
        mapActivationCancellation = null;
        MapView.DeactivateAsync().SafeFireAndForget(OnMapDeactivationFailed);
        ViewModel.Map.Deactivate();
        base.OnUnloaded(e);
    }

    private async Task ActivateMapAsync(CancellationToken cancellationToken)
    {
        MapLoadingIndicator.IsVisible = true;
        try
        {
            await ViewModel.Map.ActivateAsync();
            cancellationToken.ThrowIfCancellationRequested();
            await MapView.ActivateAsync(ViewModel.Map);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                MapLoadingIndicator.IsVisible = false;
            }
        }
    }

    private void OnMapActivationFailed(Exception exception)
    {
        if (exception is not OperationCanceledException)
        {
            Logger.LogError(exception, "Flight Planner map activation failed");
        }
    }

    private void OnMapDeactivationFailed(Exception exception)
    {
        Logger.LogError(exception, "Flight Planner map deactivation failed");
    }
}
