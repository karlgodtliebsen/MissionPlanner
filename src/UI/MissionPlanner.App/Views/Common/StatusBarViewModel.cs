using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Configuration;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.Common;

/// <summary>
/// ViewModel for the global status bar
/// </summary>
public partial class StatusBarViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<StatusBarViewModel> logger;
    private readonly ApplicationStateService stateService;
    private readonly IDispatcher dispatcher;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IVehicleParameterLoadStatusContext parameterLoadStatus;
    private readonly IList<IDisposable> disposables = [];
    private readonly IActiveVehicleContext activeVehicle;

    private bool isDisposed;
    private IDispatcherTimer? timer;

    /// <summary>
    /// 
    /// </summary>
    [ObservableProperty] public partial string StatusMessage { get; set; } = "Ready";

    /// <summary>
    /// 
    /// </summary>
    [ObservableProperty]
    public partial bool IsConnectedStatus
    {
        get; set;
    }


    /// <summary>
    /// 
    /// </summary>
    [ObservableProperty] public partial string CurrentTime { get; set; } = DateTime.Now.ToString("HH:mm:ss");


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



    /// <summary>Short feedback message for the last menu action.</summary>
    [ObservableProperty]
    public partial bool HasStatusMessage
    {
        get; set;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusBarViewModel"/> class.
    /// </summary>
    /// <param name="stateService">The application state service.</param>
    /// <param name="activeVehicle">The active vehicle context.</param>
    /// <param name="dispatcher">The Dispatcher for UI thread operations.</param>
    /// <param name="domainEventHub">The domain event hub.</param>
    /// <param name="dateTimeProvider">The date time provider.</param>
    /// <param name="parameterLoadStatus"></param>
    /// <param name="logger">The logger instance.</param>
    public StatusBarViewModel(
        ApplicationStateService stateService,
        IActiveVehicleContext activeVehicle,
        IDispatcher dispatcher,
        IDomainEventHub domainEventHub,
        IDateTimeProvider dateTimeProvider,
        IVehicleParameterLoadStatusContext parameterLoadStatus,
        ILogger<StatusBarViewModel> logger)
    {
        this.logger = logger;
        this.stateService = stateService;
        this.activeVehicle = activeVehicle;
        this.dispatcher = dispatcher;
        this.dateTimeProvider = dateTimeProvider;
        this.parameterLoadStatus = parameterLoadStatus;


        activeVehicle.Changed += OnActiveVehicleChanged;

        // Subscribe to connection state changes
        stateService.PropertyChanged += OnApplicationStateChanged;

        // Subscribe to vehicle connection events

        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(OnVehicleConnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleDisconnected>(OnVehicleDisconnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleParameterLoadStatusChanged>(OnVehicleLoadStatusChanged));


        // Start clock timer
        StartClock();

        // Initial state
        UpdateConnectionStatus();
        if (stateService.VehicleId is { } vehicleId && parameterLoadStatus.Get(vehicleId) is { } status)
        {
            StatusMessage = FormatParameterLoadStatus(status);
        }
        UpdateVehicleStatus(activeVehicle.Current);
    }

    private Task OnVehicleLoadStatusChanged(VehicleParameterLoadStatusChanged evt, CancellationToken cancellationToken)
    {
        dispatcher.Dispatch(() =>
        {
            var latest = parameterLoadStatus.Get(evt.Status.VehicleId);
            if (stateService.VehicleId == evt.Status.VehicleId && latest == evt.Status)
            {
                StatusMessage = FormatParameterLoadStatus(latest);
            }
        });
        return Task.CompletedTask;
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs e)
    {
        dispatcher.Dispatch(() => UpdateVehicleStatus(e.Current));
    }

    private void UpdateVehicleStatus(ActiveVehicleSnapshot snapshot)
    {
        dispatcher.Dispatch(() =>
        {
            VehicleDisplayName = snapshot.DisplayName;
            ConnectionStatus = snapshot.State?.ConnectionState.ToString() ?? "Offline";
            TelemetryFreshness = snapshot.State is null
                ? "Telemetry: unavailable"
                : $"Telemetry: {FormatAge(snapshot.State.LastHeartbeatAt)}";
        });
    }

    private Task OnVehicleStateUpdated(VehicleStateUpdated evt, CancellationToken cancellationToken)
    {
        if (evt.VehicleId == activeVehicle.VehicleId)
        {
            dispatcher.Dispatch(() =>
            {
                if (evt.VehicleId == activeVehicle.VehicleId)
                {
                    UpdateVehicleStatus(new ActiveVehicleSnapshot(evt.VehicleId, evt.VehicleState));
                }
            });
        }

        return Task.CompletedTask;
    }
    private string FormatAge(DateTimeOffset observedAt)
    {
        var age = dateTimeProvider.UtcNow - observedAt;
        return age <= TimeSpan.FromSeconds(2)
            ? "live"
            : age < TimeSpan.FromMinutes(1)
                ? $"{Math.Max(0, (int)age.TotalSeconds)}s old"
                : $"{Math.Max(0, (int)age.TotalMinutes)}m old";
    }

    private static string FormatParameterLoadStatus(ParameterLoadStatus status)
    {
        return status.Message;
    }

    //private void StartClock()
    //{
    //    timer = dispatcher.CreateTimer();
    //    if (timer != null)
    //    {
    //        timer.Interval = TimeSpan.FromSeconds(1);
    //        timer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("HH:mm:ss");
    //        timer.Start();
    //    }
    //}

    private void StartClock()
    {
        timer = dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(1);
        timer.Tick += OnClockTick;
        timer.Start();
    }

    private void OnClockTick(object? sender, EventArgs e)
    {
        if (!isDisposed)
        {
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        }
    }



    private void UpdateConnectionStatus()
    {
        if (stateService.IsConnected)
        {
            ConnectionStatus = "Connected";
            IsConnectedStatus = true;
        }
        else
        {
            ConnectionStatus = "Disconnected";
            IsConnectedStatus = false;
        }
    }
    private void OnApplicationStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ApplicationStateService.IsConnected))
        {
            UpdateConnectionStatus();
        }
    }
    private Task OnVehicleDisconnected(VehicleDisconnected evt, CancellationToken ct)
    {
        dispatcher.Dispatch(() =>
        {
            ConnectionStatus = $"Disconnected: {evt.VehicleId}";
            IsConnectedStatus = false;
            StatusMessage = $"Vehicle {evt.VehicleId} disconnected";
        });
        return Task.CompletedTask;
    }

    private Task OnVehicleConnected(VehicleConnected evt, CancellationToken ct)
    {
        dispatcher.Dispatch(() =>
        {
            ConnectionStatus = $"Connected: {evt.VehicleId}";
            IsConnectedStatus = true;
            StatusMessage = $"Vehicle {evt.VehicleId} connected via {evt.ConnectionType}";
        });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }
        isDisposed = true;
        if (timer is not null)
        {
            timer.Tick -= OnClockTick;
            timer.Stop();
            timer = null;
        }
        stateService.PropertyChanged -= OnApplicationStateChanged;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }

        disposables.Clear();
    }
}
