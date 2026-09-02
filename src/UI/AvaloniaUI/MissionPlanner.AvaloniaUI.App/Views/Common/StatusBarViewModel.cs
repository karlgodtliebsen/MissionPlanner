using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Configuration;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.AvaloniaUI.App.Views.Common;

/// <summary>
/// ViewModel for the global status bar
/// </summary>
public partial class StatusBarViewModel : ViewModelBase
{
    private readonly ApplicationStateService stateService;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IVehicleParameterLoadStatusContext parameterLoadStatus;
    private readonly IList<IDisposable> disposables = [];
    private readonly IActiveVehicleContext activeVehicle;
    private bool isDisposed;
    private System.Threading.Timer? timer;

    /// <summary>
    /// 
    /// </summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Ready";

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
    [ObservableProperty]
    public new partial bool HasStatusMessage
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

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusBarViewModel"/> class.
    /// </summary>
    /// <param name="stateService">The application state service.</param>
    /// <param name="activeVehicle">The active vehicle context.</param>
    /// <param name="domainEventHub">The domain event hub.</param>
    /// <param name="dateTimeProvider">The date time provider.</param>
    /// <param name="parameterLoadStatus"></param>
    /// <param name="logger">The logger instance.</param>
    public StatusBarViewModel(
        ApplicationStateService stateService,
        IActiveVehicleContext activeVehicle,
        IDomainEventHub domainEventHub,
        IDateTimeProvider dateTimeProvider,
        IVehicleParameterLoadStatusContext parameterLoadStatus,
        ILogger<StatusBarViewModel> logger) : base(logger)
    {
        this.stateService = stateService;
        this.activeVehicle = activeVehicle;
        this.dateTimeProvider = dateTimeProvider;
        this.parameterLoadStatus = parameterLoadStatus;

        activeVehicle.Changed += OnActiveVehicleChanged;
        stateService.PropertyChanged += OnApplicationStateChanged;
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleStateUpdated>(OnVehicleStateUpdated));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(OnVehicleConnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleDisconnected>(OnVehicleDisconnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleParameterLoadStatusChanged>(OnVehicleLoadStatusChanged));

        disposables.Add(domainEventHub.SubscribeDomainEventAsync<StatusMessageReceived>(OnStatusMessageReceived));

        // Start clock timer
        StartClock();

        // Initial state
        UpdateConnectionStatus();
        if (stateService.VehicleId is { } vehicleId && parameterLoadStatus.Get(vehicleId) is { } status)
        {
            StatusMessage = FormatParameterLoadStatus(status);
            HasStatusMessage = !string.IsNullOrEmpty(StatusMessage);
        }
        UpdateVehicleStatus(activeVehicle.Current);
    }

    private Task OnStatusMessageReceived(StatusMessageReceived msg, CancellationToken arg2)
    {
        Dispatcher.Dispatch(() =>
        {
            StatusMessage = msg.Message ?? "Ready";
            HasStatusMessage = !string.IsNullOrEmpty(StatusMessage);
        });
        return Task.CompletedTask;
    }

    private Task OnVehicleLoadStatusChanged(VehicleParameterLoadStatusChanged evt, CancellationToken cancellationToken)
    {
        Dispatcher.Dispatch(() =>
        {
            var latest = parameterLoadStatus.Get(evt.Status.VehicleId);
            if (stateService.VehicleId == evt.Status.VehicleId && latest == evt.Status)
            {
                StatusMessage = FormatParameterLoadStatus(latest);
                HasStatusMessage = !string.IsNullOrEmpty(StatusMessage);
            }
            if (StatusMessage.StartsWith("Loaded ") && NotificationManager is not null)
            {
                NotificationManager!.Show(StatusMessage);
            }
        });
        return Task.CompletedTask;
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs e)
    {
        Dispatcher.Dispatch(() => UpdateVehicleStatus(e.Current));
    }

    private void UpdateVehicleStatus(ActiveVehicleSnapshot snapshot)
    {
        Dispatcher.Dispatch(() =>
        {
            VehicleDisplayName = snapshot.DisplayName;
            ConnectionStatus = snapshot.State?.ConnectionState.ToString() ?? "Offline";
            TelemetryFreshness = snapshot.State is null
                ? "Telemetry: unavailable"
                : $"Telemetry: {FormatAge(snapshot.State.LastHeartbeatAt)}";
            HasStatusMessage = !string.IsNullOrEmpty(StatusMessage);
        });
    }

    private async Task OnVehicleStateUpdated(VehicleStateUpdated evt, CancellationToken cancellationToken)
    {
        if (evt.VehicleId == activeVehicle.VehicleId)
        {
            await Dispatcher.DispatchAsync(() =>
            {
                if (evt.VehicleId == activeVehicle.VehicleId)
                {
                    UpdateVehicleStatus(new ActiveVehicleSnapshot(evt.VehicleId, evt.VehicleState));
                }
            });
        }
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
    private void StartClock()
    {
        timer = new Timer(OnClockTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    private void OnClockTick(object? state)
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
    private async Task OnVehicleDisconnected(VehicleDisconnected evt, CancellationToken ct)
    {
        await Dispatcher.DispatchAsync(() =>
        {
            ConnectionStatus = $"Disconnected: {evt.VehicleId}";
            IsConnectedStatus = false;
            StatusMessage = $"Vehicle {evt.VehicleId} disconnected";
            HasStatusMessage = !string.IsNullOrEmpty(StatusMessage);
        });
    }

    private async Task OnVehicleConnected(VehicleConnected evt, CancellationToken ct)
    {
        await Dispatcher.DispatchAsync(() =>
        {
            ConnectionStatus = $"Connected: {evt.VehicleId}";
            IsConnectedStatus = true;
            StatusMessage = $"Vehicle {evt.VehicleId} connected via {evt.ConnectionType}";
            HasStatusMessage = true;
        });
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        if (isDisposed)
        {
            return;
        }
        isDisposed = true;
        timer?.Dispose();
        timer = null;
        stateService.PropertyChanged -= OnApplicationStateChanged;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }

        disposables.Clear();
        base.Dispose();
    }
}
