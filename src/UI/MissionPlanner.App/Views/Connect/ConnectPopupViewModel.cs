using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.App.Configuration;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.Connect;

public partial class ConnectPopupViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ILogger<ConnectPopupViewModel> logger;
    private readonly ISerialPortDiscoveryService portDiscovery;
    private readonly IVehicleConnectionService connectionService;
    private readonly IDomainEventHub eventHub;
    private readonly IDisposable eventSubscription;
    private readonly ApplicationStateService stateService;
    private readonly IDispatcher dispatcher;

    public ObservableRangeCollection<string> Channels { get; set; }

    public ObservableRangeCollection<string> BaudRates { get; set; }

    [ObservableProperty] public partial string? SelectedHost { get; set; }

    [ObservableProperty] public partial string? SelectedChannel { get; set; }

    [ObservableProperty] public partial string? SelectedPort { get; set; }

    [ObservableProperty] public partial string? VehicleName { get; set; }
    [ObservableProperty] public partial string? SelectedBaudRate { get; set; }

    [ObservableProperty] public partial bool IsConnected { get; set; }

    [ObservableProperty] public partial string? IsConnectedImage { get; set; } = ConnectImage;

    [ObservableProperty] public partial bool IsConnecting { get; set; }

    [ObservableProperty] public partial string? StatusMessage { get; set; }
    [ObservableProperty] public partial bool ShowSelectedHost { get; set; }
    [ObservableProperty] public partial bool ShowSelectedCom { get; set; } = true;

    private List<string> configuredChannels { get; set; }
    private const string? ConnectImage = "Resources/Images/x_light_disconnect_icon_x.png";
    private const string? DisConnectImage = "Resources/Images/x_light_connect_icon_x.png";

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectPopupViewModel"/> class with the specified application state and options.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="stateService"></param>
    /// <param name="portDiscovery"></param>
    /// <param name="connectionService"></param>
    /// <param name="eventHub"></param>
    /// <param name="dispatcher"></param>
    /// <param name="logger"></param>
    public ConnectPopupViewModel(
        ISerialPortDiscoveryService portDiscovery,
        IVehicleConnectionService connectionService,
        IDomainEventHub eventHub,
        ApplicationStateService stateService,
        IOptionsMonitor<ApplicationOptions> options,
        IDispatcher dispatcher,
        ILogger<ConnectPopupViewModel> logger)
    {
        this.logger = logger;
        this.portDiscovery = portDiscovery;
        this.connectionService = connectionService;
        this.eventHub = eventHub;
        this.stateService = stateService;
        this.dispatcher = dispatcher;
        configuredChannels = options.CurrentValue.Channels.ToList();
        Channels = new ObservableRangeCollection<string>(configuredChannels);
        BaudRates = new ObservableRangeCollection<string>(options.CurrentValue.BaudRates);
        SelectedHost = options.CurrentValue.Host;
        SelectedPort = options.CurrentValue.Port;
        SelectedChannel = stateService.SelectedChannel;
        SelectedBaudRate = stateService.SelectedBaudRate;
        IsConnected = stateService.IsConnected;

        //  connectionService.DisconnectAsync(CancellationToken.None);

        // Subscribe to state changes
        stateService.PropertyChanged += (sender, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(ApplicationStateService.SelectedChannel):
                    if (SelectedChannel != stateService.SelectedChannel)
                    {
                        SelectedChannel = stateService.SelectedChannel;
                        ShowSelectedHost = SelectedChannel is "TCP" or "UDP" or "UDPCI";
                        ShowSelectedCom = !ShowSelectedHost;
                    }

                    break;
                case nameof(ApplicationStateService.SelectedBaudRate):
                    if (SelectedBaudRate != stateService.SelectedBaudRate)
                    {
                        SelectedBaudRate = stateService.SelectedBaudRate;
                    }

                    break;
                case nameof(ApplicationStateService.SelectedPort):
                    if (SelectedPort != stateService.SelectedPort)
                    {
                        SelectedPort = stateService.SelectedPort;
                    }

                    break;
                case nameof(ApplicationStateService.SelectedHost):
                    if (SelectedHost != stateService.SelectedHost)
                    {
                        SelectedHost = stateService.SelectedHost;
                    }

                    break;
                case nameof(ApplicationStateService.IsConnected):
                    IsConnected = stateService.IsConnected;
                    UpdateConnectionStatus();
                    break;
            }
        };

        // Subscribe to connection events
        eventSubscription = eventHub.SubscribeDomainEventAsync<VehicleConnected>(OnVehicleConnected);

        // Initialize port list
        RefreshPortList();
        UpdateConnectionStatus();
    }


    private void UpdateConnectionStatus()
    {
        dispatcher.Dispatch(() => IsConnectedImage = stateService.IsConnected ? ConnectImage : DisConnectImage);
    }

    /// <summary>
    /// Refreshes the list of available serial ports
    /// </summary>
    private void RefreshPortList()
    {
        try
        {
            var availablePorts = portDiscovery.GetAvailablePorts();
            Channels.Clear();
            var selectedChannel = SelectedChannel;
            if (availablePorts.Length > 0)
            {
                var channels = availablePorts.Concat(configuredChannels).Distinct().Order().ToArray();

                Channels.AddRange(channels);
                SelectedChannel = availablePorts[0];
            }
            else
            {
                Channels.AddRange(configuredChannels);
                SelectedChannel = selectedChannel;
            }

            logger.LogInformation("Refreshed port list: {PortCount} ports found", availablePorts.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh port list");
            StatusMessage = "Error detecting ports";
            SelectedChannel = null;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        switch (args.PropertyName)
        {
            case nameof(SelectedChannel):
                if (SelectedChannel != stateService.SelectedChannel)
                {
                    stateService?.SelectedChannel = SelectedChannel!;
                    ShowSelectedHost = SelectedChannel is "TCP" or "UDP" or "UDPCI";
                    ShowSelectedCom = !ShowSelectedHost;
                }

                break;
            case nameof(SelectedPort):
                if (SelectedPort != stateService.SelectedPort)
                {
                    stateService?.SelectedPort = SelectedPort!;
                }

                break;
            case nameof(SelectedHost):
                if (SelectedHost != stateService.SelectedHost)
                {
                    stateService?.SelectedHost = SelectedHost!;
                }

                break;
            case nameof(SelectedBaudRate):
                if (SelectedBaudRate != stateService.SelectedBaudRate)
                {
                    stateService?.SelectedBaudRate = SelectedBaudRate!;
                }

                break;
            case nameof(IsConnected):
                if (IsConnected != stateService.IsConnected)
                {
                    stateService?.IsConnected = IsConnected;
                    UpdateConnectionStatus();
                }

                break;
        }
    }


    [RelayCommand]
    private void Refresh()
    {
        RefreshPortList();
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
            await DisconnectAsync();
            return;
        }

        if (SelectedChannel is null)
        {
            return;
        }

        IsConnecting = true;
        StatusMessage = "Connecting...";

        try

        {
            var selection = SelectedChannel.ToLowerInvariant();

            // Auto-detect connection type based on port name
            if (selection.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                selection.StartsWith("/dev/tty", StringComparison.OrdinalIgnoreCase))
            {
                selection = "serial";
            }
            else if (selection.Contains(":"))
            {
                selection = "tcp";
            }
            else
            {
                selection = "udp"; // Default to UDP if unknown
            }

            logger.LogInformation("Connecting to vehicle using transport: {transport}", selection);

            var result = selection switch
            {
                "serial" => await ConnectSerialAsync(),
                "tcp" => await ConnectTcpAsync(),
                "udp" => await ConnectUdpAsync(),
                var _ => new VehicleConnectionResult(false, null, null, "Unsupported connection type")
            };

            if (result.Success && result.VehicleId.HasValue)
            {
                SuccessConnection(result.VehicleId.Value);
            }
            else
            {
                await DisconnectAsync();
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
            UpdateConnectionStatus();
        }
    }

    private void SuccessConnection(VehicleId vehicleId)
    {
        dispatcher.Dispatch(() =>
            {
                IsConnected = true;
                IsConnectedImage = DisConnectImage;
                StatusMessage = $"Connected to vehicle {vehicleId}";
                logger.LogInformation("Successfully connected to vehicle {VehicleId}", vehicleId);
            }
        );
    }

    private void Disconnected()
    {
        UpdateConnectionStatus();
        dispatcher.Dispatch(() =>
            {
                IsConnected = false;
                StatusMessage = $"Disconnected from vehicle";
            }
        );
        logger.LogInformation("Disconnected from vehicle");
    }

    private async Task DisconnectAsync()
    {
        try
        {
            Disconnected();
            await dispatcher.DispatchAsync(async () =>
            {
                StatusMessage = "Disconnecting...";
                await connectionService.DisconnectAsync();
                StatusMessage = "Disconnected";
                UpdateConnectionStatus();
            });
            logger.LogInformation("Disconnected from all vehicles");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during disconnect");
            StatusMessage = $"Disconnect error: {ex.Message}";
        }
    }

    private async Task<VehicleConnectionResult> ConnectSerialAsync()
    {
        return string.IsNullOrEmpty(SelectedChannel)
            ? new VehicleConnectionResult(false, null, null, "No channel selected")
            : !int.TryParse(SelectedBaudRate, out var baudRate)
                ? new VehicleConnectionResult(false, null, null, "Invalid baud rate")
                : await connectionService.ConnectSerialAsync(SelectedChannel, baudRate);
    }

    private async Task<VehicleConnectionResult> ConnectTcpAsync()
    {
        var host = SelectedHost;
        if (host is null)
        {
            StatusMessage = "Host not specified";
            return new VehicleConnectionResult(false, null, null, "Host not specified");
        }

        var port = SelectedPort;
        if (port is null)
        {
            StatusMessage = "Port not specified";
            return new VehicleConnectionResult(false, null, null, "Port not specified");
        }

        var p = int.TryParse(port, out var portNumber);
        if (!p)
        {
            StatusMessage = "Invalid port number";
            return new VehicleConnectionResult(false, null, null, "Invalid port number");
        }

        return await connectionService.ConnectTcpAsync(host, portNumber);
    }

    private async Task<VehicleConnectionResult> ConnectUdpAsync()
    {
        // For UDP, use the baud rate field as the local port (or a separate field in real UI)
        if (!int.TryParse(SelectedPort, out var localPort))
        {
            localPort = 14550; // Default UDP port
        }

        return await connectionService.ConnectUdpAsync(localPort);
    }

    private async Task OnVehicleConnected(VehicleConnected evt, CancellationToken ct)
    {
        // Update UI on main thread
        await dispatcher.DispatchAsync(async () =>
        {
            IsConnected = true;
            UpdateConnectionStatus();
            StatusMessage = $"Vehicle {evt.VehicleId} connected via {evt.ConnectionType}";
        });
    }


    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await connectionService.DisposeAsync().ConfigureAwait(false);
    }
}
