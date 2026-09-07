using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Advanced;
using MissionPlanner.Core.Setup.Advanced.Proximity;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Proximity;

/// <summary>Coordinates proximity data with coalesced four-Hz view updates and owned observation lifetime.</summary>
public sealed partial class ProximityViewModel(ProximitySession session, ProximityRadarViewModel radar,
    ProximityDiagnosticsViewModel diagnostics, TimeProvider clock, ILogger<ProximityViewModel> logger,
    IUiDispatcher dispatcher, IDomainEventHub events) : ViewModelBase(logger, dispatcher, events)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private AdvancedToolLifetime? lifetime;
    /// <summary>Gets the radar panel.</summary>
    public ProximityRadarViewModel Radar => radar;
    /// <summary>Gets the diagnostic panel.</summary>
    public ProximityDiagnosticsViewModel Diagnostics => diagnostics;
    /// <summary>Gets the nearest obstacle and diagnostic summary.</summary>
    [ObservableProperty]
    public partial string Summary { get; private set; } = "No proximity observations received.";

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (lifetime is not null)
            {
                return;
            }
            var activation = new AdvancedToolLifetime();
            lifetime = activation;
            activation.OnClosing(async () =>
            {
                await session.StopAsync();
                await Dispatcher.DispatchAsync(() =>
                {
                    radar.Snapshot = ProximitySnapshot.Empty;
                    diagnostics.Points = [];
                    Summary = "Proximity stopped.";
                });
            });
            session.Start(activation.Token);
            var updates = UpdateAsync(activation.Token);
            activation.OnClosing(async () => await updates);
        }
        catch (Exception exception)
        {
            if (lifetime is not null)
            {
                await lifetime.DisposeAsync();
                lifetime = null;
            }
            Summary = "Proximity could not start. Connect a vehicle and reopen the page.";
            Logger.LogWarning("Proximity startup failed ({FailureType}).", exception.GetType().Name);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        lifetime?.Cancel();
        await gate.WaitAsync();
        try
        {
            if (lifetime is not null)
            {
                await lifetime.DisposeAsync();
                lifetime = null;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        lifetime?.Cancel();
        _ = CloseAsync();
        base.Dispose();
    }

    private async Task CloseAsync()
    {
        try
        {
            await DeactivateAsync();
        }
        catch (Exception exception)
        {
            Logger.LogWarning("Proximity cleanup failed ({FailureType}).", exception.GetType().Name);
        }
    }

    private async Task UpdateAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250), clock);
            while (await timer.WaitForNextTickAsync(token))
            {
                var snapshot = session.Snapshot();
                await Dispatcher.DispatchAsync(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }
                    radar.Snapshot = snapshot;
                    diagnostics.Points = snapshot.Points;
                    var closest = snapshot.Nearest is { } nearest
                        ? $"Nearest: {nearest.DistanceMeters:F2} m at {nearest.BearingDegrees:F0} degrees clockwise from forward."
                        : "No current valid horizontal obstacle; this does not indicate clear space.";
                    Summary = $"{closest} Too close: {snapshot.Points.Count(point => point.State == ProximitySampleState.TooClose)}; "
                        + $"malformed: {snapshot.Malformed}; unsupported: {snapshot.Unsupported}; dropped: {snapshot.Dropped}.";
                });
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }
}
