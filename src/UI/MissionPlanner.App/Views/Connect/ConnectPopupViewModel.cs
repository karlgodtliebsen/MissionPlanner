using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.App.Configuration;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Services;

namespace MissionPlanner.App.Views.Connect;

public partial class ConnectPopupViewModel : ObservableObject
{
    private readonly ILogger<ConnectPopupViewModel> logger;
    private readonly ISerialPortDiscoveryService portDiscovery;
    private readonly IVehicleConnectionService connectionService;
    private readonly IDomainEventHub eventHub;
    private readonly IDisposable? eventSubscription;

    /// <summary>
    /// Gets the application options.
    /// </summary>
    public ApplicationOptions Options { get; } = null!;

    private readonly ApplicationStateService? stateService;

    [ObservableProperty] public partial string? SelectedConnectionType { get; set; }

    [ObservableProperty] public partial string? SelectedPort { get; set; }

    [ObservableProperty] public partial string? SelectedBaudRate { get; set; }

    [ObservableProperty] public partial bool IsConnected { get; set; }

    [ObservableProperty] public partial string? IsConnectedImage { get; set; } = ConnectImage;

    [ObservableProperty] public partial bool IsConnecting { get; set; }

    [ObservableProperty] public partial string? StatusMessage { get; set; }


    private const string? ConnectImage = "Resources/Images/light_disconnect_icon.png";
    private const string? DisConnectImage = "Resources/Images/light_connect_icon.png";

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectPopupViewModel"/> class with the specified application state and options.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="stateService"></param>
    /// <param name="portDiscovery"></param>
    /// <param name="connectionService"></param>
    /// <param name="eventHub"></param>
    /// <param name="logger"></param>
    public ConnectPopupViewModel(
        ISerialPortDiscoveryService portDiscovery,
        IVehicleConnectionService connectionService,
        IDomainEventHub eventHub,
        ApplicationStateService stateService,
        IOptionsMonitor<ApplicationOptions> options,
        ILogger<ConnectPopupViewModel> logger)
    {
        this.logger = logger;
        this.portDiscovery = portDiscovery;
        this.connectionService = connectionService;
        this.eventHub = eventHub;

        Options = options.CurrentValue;
        this.stateService = stateService;

        // Initialize from shared state
        SelectedConnectionType = stateService.SelectedConnectionType ?? "Serial";
        SelectedPort = stateService.SelectedPort;
        SelectedBaudRate = stateService.SelectedBaudRate ?? "57600";
        IsConnected = stateService.IsConnected;

        // Subscribe to state changes
        stateService.PropertyChanged += (sender, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(ApplicationStateService.SelectedConnectionType):
                    SelectedConnectionType = stateService.SelectedConnectionType;
                    break;
                case nameof(ApplicationStateService.SelectedPort):
                    SelectedPort = stateService.SelectedPort;
                    break;
                case nameof(ApplicationStateService.SelectedBaudRate):
                    SelectedBaudRate = stateService.SelectedBaudRate;
                    break;
                case nameof(ApplicationStateService.IsConnected):
                    IsConnected = stateService.IsConnected;
                    IsConnectedImage = IsConnected ? DisConnectImage : ConnectImage;
                    break;
            }
        };

        // Subscribe to connection events
        eventSubscription = eventHub.SubscribeDomainEvent<VehicleConnected>(OnVehicleConnected);

        // Initialize port list
        RefreshPortList();
    }

    /// <summary>
    /// Refreshes the list of available serial ports
    /// </summary>
    public void RefreshPortList()
    {
        try
        {
            var availablePorts = portDiscovery.GetAvailablePorts();
            Options.Ports.Clear();

            if (availablePorts.Length > 0)
            {
                foreach (var port in availablePorts)
                {
                    Options.Ports.Add(port);
                }

                // Auto-select first port if nothing is selected
                if (string.IsNullOrEmpty(SelectedPort) && Options.Ports.Count > 0)
                {
                    SelectedPort = Options.Ports[0];
                }
            }
            else
            {
                Options.Ports.Add("No ports found");
                StatusMessage = "No serial ports detected";
            }

            logger.LogInformation("Refreshed port list: {PortCount} ports found", availablePorts.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh port list");
            StatusMessage = "Error detecting ports";
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        switch (args.PropertyName)
        {
            case nameof(SelectedConnectionType):
                stateService?.SelectedConnectionType = SelectedConnectionType!;
                break;
            case nameof(SelectedPort):
                stateService?.SelectedPort = SelectedPort!;
                break;
            case nameof(SelectedBaudRate):
                stateService?.SelectedBaudRate = SelectedBaudRate!;
                break;
            case nameof(IsConnected):
                stateService?.IsConnected = IsConnected;
                break;
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (IsConnecting)
        {
            return;
        }

        if (IsConnected)
        {
            // Disconnect
            await DisconnectAsync();
            return;
        }

        // Validate inputs
        if (string.IsNullOrEmpty(SelectedConnectionType))
        {
            StatusMessage = "Please select a connection type";
            return;
        }

        IsConnecting = true;
        StatusMessage = "Connecting...";

        try
        {
            var result = SelectedConnectionType?.ToLowerInvariant() switch
            {
                "serial" => await ConnectSerialAsync(),
                "tcp" => await ConnectTcpAsync(),
                "udp" => await ConnectUdpAsync(),
                var _ => new VehicleConnectionResult(false, null, "Unsupported connection type")
            };

            if (result.Success)
            {
                IsConnected = true;
                IsConnectedImage = DisConnectImage;
                StatusMessage = $"Connected to vehicle {result.VehicleId}";
                logger.LogInformation("Successfully connected to vehicle {VehicleId}", result.VehicleId);
            }
            else
            {
                StatusMessage = $"Connection failed: {result.ErrorMessage}";
                logger.LogWarning("Connection failed: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during connection attempt");
            StatusMessage = $"Connection error: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private async Task<VehicleConnectionResult> ConnectSerialAsync()
    {
        return string.IsNullOrEmpty(SelectedPort)
            ? new VehicleConnectionResult(false, null, "No port selected")
            : !int.TryParse(SelectedBaudRate, out var baudRate)
                ? new VehicleConnectionResult(false, null, "Invalid baud rate")
                : await connectionService.ConnectSerialAsync(SelectedPort, baudRate);
    }

    private async Task<VehicleConnectionResult> ConnectTcpAsync()
    {
        // For TCP, the "port" field should contain host:port
        // This is a simplified implementation - you might want a separate host/port UI
        var parts = SelectedPort?.Split(':');
        return parts?.Length != 2 || !int.TryParse(parts[1], out var port)
            ? new VehicleConnectionResult(false, null, "TCP format should be host:port")
            : await connectionService.ConnectTcpAsync(parts[0], port);
    }

    private async Task<VehicleConnectionResult> ConnectUdpAsync()
    {
        // For UDP, use the baud rate field as the local port (or a separate field in real UI)
        if (!int.TryParse(SelectedBaudRate, out var localPort))
        {
            localPort = 14550; // Default UDP port
        }

        return await connectionService.ConnectUdpAsync(localPort);
    }

    private async Task DisconnectAsync()
    {
        try
        {
            StatusMessage = "Disconnecting...";
            await connectionService.DisconnectAllAsync();
            IsConnected = false;
            IsConnectedImage = ConnectImage;
            StatusMessage = "Disconnected";
            logger.LogInformation("Disconnected from all vehicles");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during disconnect");
            StatusMessage = $"Disconnect error: {ex.Message}";
        }
    }

    private void OnVehicleConnected(VehicleConnected evt)
    {
        // Update UI on main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsConnected = true;
            IsConnectedImage = DisConnectImage;
            StatusMessage = $"Vehicle {evt.VehicleId} connected via {evt.ConnectionType}";
        });
    }
}
