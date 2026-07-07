using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Configuration;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.Common;

/// <summary>
/// ViewModel for the global status bar
/// </summary>
public partial class StatusBarViewModel : ObservableObject
{
    private readonly ILogger<StatusBarViewModel> logger;
    private readonly ApplicationStateService stateService;
    private readonly IDomainEventHub? eventHub;
    private readonly IDisposable? eventSubscription;
    private IDispatcherTimer? timer;

    [ObservableProperty] public partial string StatusMessage { get; set; } = "Ready";

    [ObservableProperty] public partial string ConnectionStatus { get; set; } = "Disconnected";

    [ObservableProperty] public partial Color ConnectionDotColor { get; set; } = Colors.Gray;

    [ObservableProperty] public partial string CurrentTime { get; set; } = DateTime.Now.ToString("HH:mm:ss");

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusBarViewModel"/> class.
    /// </summary>
    public StatusBarViewModel()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusBarViewModel"/> class.
    /// </summary>
    /// <param name="stateService">The application state service.</param>
    /// <param name="eventHub">The domain event hub.</param>
    /// <param name="logger">The logger instance.</param>
    public StatusBarViewModel(ApplicationStateService stateService, IDomainEventHub eventHub, ILogger<StatusBarViewModel> logger) : this()
    {
        this.logger = logger;
        this.stateService = stateService;
        this.eventHub = eventHub;

        // Subscribe to connection state changes
        stateService.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ApplicationStateService.IsConnected))
            {
                UpdateConnectionStatus();
            }
        };

        // Subscribe to vehicle connection events

        eventSubscription = eventHub.SubscribeDomainEventAsync<VehicleConnected>(OnVehicleConnected);


        // Start clock timer
        StartClock();

        // Initial state
        UpdateConnectionStatus();
    }

    private void StartClock()
    {
        timer = Application.Current?.Dispatcher.CreateTimer();
        if (timer != null)
        {
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            timer.Start();
        }
    }

    private void UpdateConnectionStatus()
    {
        if (stateService.IsConnected)
        {
            ConnectionStatus = "Connected";
            ConnectionDotColor = Colors.LimeGreen;
        }
        else
        {
            ConnectionStatus = "Disconnected";
            ConnectionDotColor = Colors.Gray;
        }
    }

    private Task OnVehicleConnected(VehicleConnected evt, CancellationToken ct)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ConnectionStatus = $"Connected: {evt.VehicleId}";
            ConnectionDotColor = Colors.LimeGreen;
            StatusMessage = $"Vehicle {evt.VehicleId} connected via {evt.ConnectionType}";
        });
        return Task.CompletedTask;
    }
}
