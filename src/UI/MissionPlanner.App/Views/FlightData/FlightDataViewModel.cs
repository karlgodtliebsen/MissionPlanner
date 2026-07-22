using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>
/// Coordinates the Flight Data page, its active tab, and active-vehicle status presentation.
/// </summary>
public partial class FlightDataViewModel : ObservableObject, IDisposable
{
    private static readonly string[] tabKeys =
    [
        "Quick", "Actions", "Messages", "PreFlight", "Gauges", "Transponder", "Status",
        "Servo/Relay", "Aux Function", "Scripts", "Payload Control", "Telemetry Logs", "DataFlash Logs"
    ];

    private readonly IActiveVehicleContext activeVehicle;
    private readonly IDateTimeProvider clock;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<FlightDataViewModel> logger;
    private readonly IReadOnlyDictionary<string, IFlightDataTabLifecycle> tabLifecycles;
    private readonly SemaphoreSlim tabTransitionLock = new(1, 1);
    private IFlightDataTabLifecycle? activeTab;
    private bool isViewActive;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightDataViewModel"/> class.
    /// </summary>
    /// <param name="activeVehicle">The shared active-vehicle context.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="tabLifecycles">The registered lifecycle-aware Flight Data tabs.</param>
    /// <param name="logger">The logger.</param>
    public FlightDataViewModel(
        IActiveVehicleContext activeVehicle,
        IDateTimeProvider clock,
        IDispatcher dispatcher,
        IEnumerable<IFlightDataTabLifecycle> tabLifecycles,
        ILogger<FlightDataViewModel> logger)
    {
        this.activeVehicle = activeVehicle;
        this.clock = clock;
        this.dispatcher = dispatcher;
        this.logger = logger;
        this.tabLifecycles = tabLifecycles.ToDictionary(tab => tab.Key, StringComparer.Ordinal);
        SelectedMapStyle = "GEO";
        UpdateVehicleStatus(activeVehicle.Current);
    }

    /// <summary>
    /// Gets the coordinate display styles offered by the map status bar.
    /// </summary>
    public IReadOnlyList<string> MapStyles { get; } = ["GEO", "UTM", "MGRS"];

    /// <summary>
    /// Gets or sets the selected coordinate display style.
    /// </summary>
    [ObservableProperty]
    public partial string SelectedMapStyle { get; set; }

    /// <summary>
    /// Gets the active vehicle display name.
    /// </summary>
    [ObservableProperty]
    public partial string VehicleDisplayName { get; private set; } = "No vehicle";

    /// <summary>
    /// Gets the active vehicle connection status.
    /// </summary>
    [ObservableProperty]
    public partial string ConnectionStatus { get; private set; } = "Offline";

    /// <summary>
    /// Gets the freshness of the latest general telemetry observation.
    /// </summary>
    [ObservableProperty]
    public partial string TelemetryFreshness { get; private set; } = "Telemetry: unavailable";

    /// <summary>
    /// Gets the freshness of the latest map-position observation.
    /// </summary>
    [ObservableProperty]
    public partial string MapFreshness { get; private set; } = "Map: no position";

    /// <summary>
    /// Activates the Flight Data page and its selected tab.
    /// </summary>
    /// <param name="selectedTabIndex">The zero-based selected tab index.</param>
    /// <param name="cancellationToken">A token that cancels activation.</param>
    public async Task ActivateAsync(int selectedTabIndex, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!isViewActive)
        {
            isViewActive = true;
            activeVehicle.Changed += OnActiveVehicleChanged;
            UpdateVehicleStatus(activeVehicle.Current);
        }

        await SelectTabAsync(selectedTabIndex, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Selects a tab and deterministically deactivates the previously visible tab.
    /// </summary>
    /// <param name="selectedTabIndex">The zero-based selected tab index.</param>
    /// <param name="cancellationToken">A token that cancels activation.</param>
    public async Task SelectTabAsync(int selectedTabIndex, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (selectedTabIndex < 0 || selectedTabIndex >= tabKeys.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedTabIndex));
        }

        await tabTransitionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var key = tabKeys[selectedTabIndex];
            tabLifecycles.TryGetValue(key, out var nextTab);
            if (ReferenceEquals(activeTab, nextTab))
            {
                return;
            }

            if (activeTab is not null)
            {
                logger.LogDebug("Deactivating Flight Data tab {TabKey}.", activeTab.Key);
                await activeTab.DeactivateAsync().ConfigureAwait(false);
            }

            activeTab = nextTab;
            if (isViewActive && activeTab is not null)
            {
                logger.LogDebug("Activating Flight Data tab {TabKey}.", activeTab.Key);
                await activeTab.ActivateAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            tabTransitionLock.Release();
        }
    }

    /// <summary>
    /// Deactivates the Flight Data page and stops work owned by its visible tab.
    /// </summary>
    public async Task DeactivateAsync()
    {
        if (disposed || !isViewActive)
        {
            return;
        }

        isViewActive = false;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        if (activeTab is not null)
        {
            await activeTab.DeactivateAsync().ConfigureAwait(false);
            activeTab = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        DeactivateAsync().GetAwaiter().GetResult();
        disposed = true;
        tabTransitionLock.Dispose();
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs e)
    {
        dispatcher.Dispatch(() => UpdateVehicleStatus(e.Current));
    }

    private void UpdateVehicleStatus(ActiveVehicleSnapshot snapshot)
    {
        VehicleDisplayName = snapshot.DisplayName;
        ConnectionStatus = snapshot.State?.ConnectionState.ToString() ?? "Offline";
        TelemetryFreshness = snapshot.State is null
            ? "Telemetry: unavailable"
            : $"Telemetry: {FormatAge(snapshot.State.LastHeartbeatAt)}";
        MapFreshness = snapshot.State?.Position.ObservedAt is { } observedAt
            ? $"Map: {FormatAge(observedAt)}"
            : "Map: no position";
    }

    private string FormatAge(DateTimeOffset observedAt)
    {
        var age = clock.UtcNow - observedAt;
        if (age <= TimeSpan.FromSeconds(2))
        {
            return "live";
        }

        return age < TimeSpan.FromMinutes(1)
            ? $"{Math.Max(0, (int)age.TotalSeconds)}s old"
            : $"{Math.Max(0, (int)age.TotalMinutes)}m old";
    }
}
