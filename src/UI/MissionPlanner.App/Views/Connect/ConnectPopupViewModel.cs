using System.ComponentModel;
using System.Net;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.App.Configuration;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.App.Views.Connect;

public partial class ConnectPopupViewModel : DialogViewModelBase
{
    private readonly ISerialPortDiscoveryService portDiscovery;
    private readonly IFirmwareSerialDeviceCatalog? serialDevices;
    private readonly IUdpVehicleDiscovery? udpDiscovery;
    private readonly IVehicleConnectionService connectionService;
    private readonly IList<IDisposable> disposables = [];
    private readonly ApplicationStateService stateService;

    /// <summary>
    /// Provides the public API for Channels.
    /// </summary>
    public ObservableRangeCollection<string> Channels
    {
        get;
        set;
    }

    /// <summary>
    /// Provides the public API for BaudRates.
    /// </summary>
    public ObservableRangeCollection<string> BaudRates
    {
        get;
        set;
    }
    [ObservableProperty]
    public partial IPAddress? Address
    {
        get; set;
    }

    partial void OnAddressChanged(IPAddress? value)
    {
        SelectedHost = value?.ToString();
    }

    [ObservableProperty]
    public partial string? SelectedHost
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? SelectedChannel
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? SelectedPort
    {
        get;
        set;
    }

    partial void OnSelectedPortChanged(string? value)
    {
        if (value is null)
        {
            SelectedIntPort = null;
            return;
        }

        if (int.TryParse(value, out var port))
        {
            if (SelectedIntPort != port)
            {
                SelectedIntPort = port;
            }
        }
        else
        {
            SelectedIntPort = null;
        }
    }

    partial void OnSelectedIntPortChanged(int? oldValue, int? newValue)
    {
        if (newValue is null)
        {
            SelectedPort = null;
            return;
        }
        var newV = newValue.Value.ToString();
        if (SelectedPort != newV)
        {
            SelectedPort = newV;
        }
    }

    [ObservableProperty]
    public partial int? SelectedIntPort
    {
        get;
        set;
    }


    [ObservableProperty]
    public partial string? VehicleName
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial VehicleId? VehicleId
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial string? SelectedBaudRate
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial bool IsConnected
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial Bitmap? ConnectionImage
    {
        get; set;
    }

    private Bitmap LoadImage(string image)
    {
        return new Bitmap(AssetLoader.Open(new Uri(image)));
    }

    [ObservableProperty]
    public partial bool IsConnecting
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial bool ShowSelectedHost
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial bool ShowSelectedCom
    {
        get;
        set;
    } = true;

    private readonly List<string> configuredChannels;
    private const string ConnectImage = "avares://MissionPlanner.App/Resources/Images/light_disconnect_icon.png";
    private const string DisConnectImage = "avares://MissionPlanner.App/Resources/Images/light_connect_icon.png";
    private readonly string defaultChannel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectPopupViewModel"/> class with the specified application state and options.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="stateService"></param>
    /// <param name="portDiscovery"></param>
    /// <param name="connectionService"></param>
    /// <param name="domainEventHub"></param>
    /// <param name="logger"></param>
    /// <param name="serialDevices">Current serial devices with USB identity for excluding non-telemetry endpoints.</param>
    /// <param name="udpDiscovery">Passive UDP discovery used only when the user connects with AUTO.</param>
    public ConnectPopupViewModel(
        ISerialPortDiscoveryService portDiscovery,
        IVehicleConnectionService connectionService,
        IDomainEventHub domainEventHub,
        ApplicationStateService stateService,
        IOptionsMonitor<ApplicationOptions> options,
        ILogger<ConnectPopupViewModel> logger,
        IFirmwareSerialDeviceCatalog? serialDevices = null,
        IUdpVehicleDiscovery? udpDiscovery = null)
    {
        this.portDiscovery = portDiscovery;
        this.serialDevices = serialDevices;
        this.udpDiscovery = udpDiscovery;
        this.connectionService = connectionService;
        this.stateService = stateService;
        configuredChannels = options.CurrentValue.Channels.ToList();
        Channels = new ObservableRangeCollection<string>(configuredChannels);
        BaudRates = new ObservableRangeCollection<string>(options.CurrentValue.BaudRates);

        if (IPAddress.TryParse(options.CurrentValue.Host, out var address))
        {
            Address = address;
        }
        SelectedHost = options.CurrentValue.Host;
        SelectedPort = options.CurrentValue.Port;
        SelectedChannel = stateService.SelectedChannel;
        defaultChannel = options.CurrentValue.Channel;
        SelectedBaudRate = stateService.SelectedBaudRate;
        IsConnected = stateService.IsConnected;
        LoadImage(ConnectImage);
        OnStateServiceChanged();

        // Subscribe to connection events
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleConnected>(OnVehicleConnected));
        disposables.Add(domainEventHub.SubscribeDomainEventAsync<VehicleDisconnected>(OnVehicleDisconnected));

        // Initialize port list
        _ = RefreshPortListAsync(preferSerial: true);
        if (IsConnected)
        {
            VehicleId = stateService.VehicleId;
            if (VehicleId is not null)
            {
                SuccessConnection(VehicleId.Value);
            }
        }

        UpdateConnectionStatus();
    }

    private void OnStateServiceChanged()
    {
        // Subscribe to state changes
        stateService.PropertyChanged += (sender, args) =>
        {
            switch (args.PropertyName)
            {
                case nameof(ApplicationStateService.SelectedChannel):
                    if (SelectedChannel != stateService.SelectedChannel)
                    {
                        SelectedChannel = stateService.SelectedChannel;
                        ShowSelectedHost = SelectedChannel is "TCP" or "UDP" or "UDPCl" or "WS" or "WSS";
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

                    if (IsConnected != stateService.IsConnected)
                    {
                        IsConnected = stateService.IsConnected;
                        UpdateConnectionStatus();
                    }

                    break;
                case nameof(ApplicationStateService.VehicleId):

                    if (VehicleId != stateService.VehicleId)
                    {
                        VehicleId = stateService.VehicleId;
                        UpdateConnectionStatus();
                    }

                    break;
                case nameof(ApplicationStateService.VehicleName):

                    if (VehicleName != stateService.VehicleName)
                    {
                        VehicleName = stateService.VehicleName;
                        UpdateConnectionStatus();
                    }

                    break;
            }
        };
    }


    private void UpdateConnectionStatus()
    {
        Dispatcher.Dispatch(() =>
        {
            ConnectionImage = stateService.IsConnected ? LoadImage(ConnectImage) : LoadImage(DisConnectImage);
            VehicleId = stateService.VehicleId;
            VehicleName = stateService.VehicleName;
            IsConnecting = false;

        });
        Task.Yield();
    }

    /// <summary>
    /// Refreshes the list of available serial ports
    /// </summary>
    private async Task<string[]> GetAvailableSerialPortsAsync()
    {
        var ports = serialDevices is null ? portDiscovery.GetAvailablePorts()
            : (await serialDevices.GetDevicesAsync())
                .Where(device => VehicleSerialConnectionPolicy.GetBlockReason(device) is null)
                .Select(device => device.PortName).ToArray();
        return ports.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task RefreshPortListAsync(bool preferSerial = false)
    {
        try
        {
            var availablePorts = await GetAvailableSerialPortsAsync();
            // Configured/stale COM entries must not bypass current device discovery.
            var channels = availablePorts.Concat(configuredChannels.Where(channel => !IsSerialChannel(channel)))
                .Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();
            await Dispatcher.DispatchAsync(() =>
            {
                Channels.ReplaceRange(channels);
                if (!IsConnected && availablePorts.Length > 0
                                 && (preferSerial || string.Equals(SelectedChannel, "AUTO", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!availablePorts.Contains(SelectedChannel, StringComparer.OrdinalIgnoreCase))
                    {
                        SelectedChannel = availablePorts[0];
                    }
                }
                else if (SelectedChannel is null || !channels.Contains(SelectedChannel, StringComparer.OrdinalIgnoreCase))
                {
                    SelectedChannel = availablePorts.FirstOrDefault()
                                      ?? (channels.Contains(defaultChannel) ? defaultChannel : channels.FirstOrDefault());
                }
            });

            Logger.LogInformation("Refreshed port list: {PortCount} ports found", availablePorts.Length);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to refresh port list");
            StatusMessage = "Error detecting ports";
            SelectedChannel = null;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (!changingNetworkDraft && args.PropertyName is nameof(SelectedHost) or nameof(SelectedPort)
            or nameof(LocalBindAddress) or nameof(ClientLocalPort) or nameof(WebSocketUrl))
        {
            OnPropertyChanged(nameof(NetworkValidationMessage));
        }
        switch (args.PropertyName)
        {
            case nameof(SelectedChannel):
                if (SelectedChannel != stateService.SelectedChannel)
                {
                    stateService?.SelectedChannel = SelectedChannel!;
                    ShowSelectedHost = SelectedChannel is "TCP" or "UDP" or "UDPCl" or "WS" or "WSS";
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
    private Task RefreshAsync()
    {
        return RefreshPortListAsync();
    }

    private static bool IsSerialChannel(string channel)
    {
        return channel.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
        || channel.StartsWith("/dev/", StringComparison.OrdinalIgnoreCase);
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

        if (!string.IsNullOrEmpty(NetworkValidationMessage))
        {
            StatusMessage = NetworkValidationMessage;
            return;
        }
        if (SelectedChannel is "UDPCl" or "WS" or "WSS")
        {
            StatusMessage = "This transport is not implemented yet. No connection was attempted.";
            return;
        }
        if (ShowUdpListen && !string.IsNullOrWhiteSpace(LocalBindAddress))
        {
            StatusMessage = "Custom UDP bind addresses are not implemented yet. Leave the bind address blank to listen on all interfaces.";
            return;
        }

        IsConnecting = true;
        StatusMessage = "Connecting...";
        if (NotificationManager is not null)
        {
            NotificationManager!.Show(StatusMessage);
        }
        await Task.Yield();
        try

        {
            var selection = SelectedChannel.ToLowerInvariant();
            IPEndPoint? discoveredUdp = null;

            if (selection == "auto")
            {
                var ports = await GetAvailableSerialPortsAsync();
                if (ports.Length > 1)
                {
                    StatusMessage = "AUTO found multiple serial devices. Select the controller's COM port.";
                    NotificationManager?.Show(StatusMessage);
                    return;
                }
                if (ports.Length == 1)
                {
                    SelectedChannel = ports[0];
                    selection = "serial";
                }
                else
                {
                    var localPort = int.TryParse(SelectedPort, out var parsedPort) ? parsedPort : 14550;
                    StatusMessage = $"AUTO: listening for an ArduPilot UDP heartbeat on port {localPort}...";
                    discoveredUdp = udpDiscovery is null ? null : await udpDiscovery.FindAsync(localPort);
                    if (discoveredUdp is null)
                    {
                        StatusMessage = $"AUTO found no available serial device or ArduPilot UDP heartbeat on port {localPort}.";
                        NotificationManager?.Show(StatusMessage);
                        return;
                    }
                    selection = "udp";
                }
            }

            // Auto-detect connection type based on port name
            if (selection == "serial" || IsSerialChannel(selection))
            {
                selection = "serial";
            }
            else if (selection == "tcp" || selection.Contains(":"))
            {
                selection = "tcp";
                SelectedChannel = "TCP";
            }
            else if (selection is "udp")
            {
                selection = "udp";
                SelectedChannel = "UDP";
            }
            else if (selection is "udpcl")
            {
                selection = "udpcl";
                SelectedChannel = "UDPCl";
            }
            else if (selection is "ws")
            {
                selection = "ws";
                SelectedChannel = "WS";
            }
            else if (selection is "wss")
            {
                selection = "wss";
                SelectedChannel = "WSS";
            }
            else
            {
                StatusMessage = $"Unsupported connection channel: {SelectedChannel}. Select a COM port, TCP, or UDP.";
                NotificationManager?.Show(StatusMessage);
                return;
            }

            Logger.LogInformation("Connecting to vehicle using transport: {transport}", selection);

            var result = selection switch
            {
                "serial" => await ConnectSerialAsync(),
                "tcp" => await ConnectTcpAsync(),
                "udp" => discoveredUdp is null ? await ConnectUdpAsync()
                    : await connectionService.ConnectUdpAsync(int.TryParse(SelectedPort, out var udpPort) ? udpPort : 14550,
                        discoveredUdp.Address.ToString(), discoveredUdp.Port),
                var _ => new VehicleConnectionResult(false, null, null, "Unsupported connection type")
            };
            await Task.Yield();
            if (result.Success && result.VehicleId.HasValue)
            {
                NotificationManager?.Show("Connected to vehicle: " + result.VehicleId.Value);
                SuccessConnection(result.VehicleId.Value);
            }
            else
            {
                await DisconnectAsync();
                StatusMessage = $"Connection failed: {result.ErrorMessage}";
                Logger.LogWarning("Connection failed: {Error}", result.ErrorMessage);
                NotificationManager?.Show(StatusMessage);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during connection attempt");
            StatusMessage = $"Connection error: {ex.Message}";
            NotificationManager?.Show(StatusMessage);

        }
        finally
        {
            UpdateConnectionStatus();
        }
    }

    private void SuccessConnection(VehicleId vehicleId)
    {
        Dispatcher.Dispatch(() =>
            {
                if (stateService.VehicleId != vehicleId)
                {
                    stateService.VehicleId = vehicleId;
                }

                if (VehicleId != vehicleId)
                {
                    VehicleId = vehicleId;
                }

                VehicleName = stateService.VehicleName;
                StatusMessage = $"Connected to {VehicleName ?? vehicleId.ToString()}";
                Logger.LogInformation("Successfully connected to vehicle {VehicleId} ({VehicleName})", vehicleId, VehicleName);
            }
        );
        Task.Yield();
    }

    private void Disconnected()
    {
        UpdateConnectionStatus();
        Dispatcher.Dispatch(() =>
            {
                IsConnected = false;
                StatusMessage = $"Disconnected from vehicle";
            }
        );
        Logger.LogInformation("Disconnected from vehicle");
    }

    private async Task DisconnectAsync()
    {
        try
        {
            Disconnected();
            await Dispatcher.DispatchAsync(async () =>
            {
                StatusMessage = "Disconnecting...";
                await connectionService.DisconnectAsync();
                StatusMessage = "Disconnected";
                if (NotificationManager is not null)
                {
                    NotificationManager!.Show(StatusMessage);
                }
                UpdateConnectionStatus();
            });
            Logger.LogInformation("Disconnected from all vehicles");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during disconnect");
            StatusMessage = $"Disconnect error: {ex.Message}";
            if (NotificationManager is not null)
            {
                NotificationManager!.Show(StatusMessage);
            }
        }
    }

    private async Task<VehicleConnectionResult> ConnectSerialAsync()
    {
        await Task.Yield();
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
        await Task.Yield();
        return await connectionService.ConnectTcpAsync(host, portNumber);
    }

    private async Task<VehicleConnectionResult> ConnectUdpAsync()
    {
        // For UDP, use the baud rate field as the local port (or a separate field in real UI)
        if (!int.TryParse(SelectedPort, out var localPort))
        {
            localPort = 14550; // Default UDP port
        }
        await Task.Yield();
        var result = await connectionService.ConnectUdpAsync(localPort);
        await Task.Yield();
        return result;
    }

    private async Task OnVehicleConnected(VehicleConnected evt, CancellationToken ct)
    {
        // Update UI on main thread
        await Dispatcher.DispatchAsync(async () =>
        {
            IsConnected = true;
            stateService.IsConnected = true;
            if (stateService.VehicleId != evt.VehicleId)
            {
                stateService.VehicleId = evt.VehicleId;
            }

            if (VehicleId != evt.VehicleId)
            {
                VehicleId = evt.VehicleId;
            }

            UpdateConnectionStatus();
            VehicleName = stateService.VehicleName;
            StatusMessage = $"{VehicleName ?? evt.VehicleId.ToString()} connected via {evt.ConnectionType}";
        });
    }

    private async Task OnVehicleDisconnected(VehicleDisconnected evt, CancellationToken ct)
    {
        // Update UI on main thread
        await Dispatcher.DispatchAsync(async () =>
        {
            VehicleId = null;
            stateService.VehicleId = null;
            IsConnected = false;
            stateService.IsConnected = false;
            UpdateConnectionStatus();
            StatusMessage = $"Vehicle {evt.VehicleId} disconnected";
        });
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Dispose();
        await connectionService.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        UpdateConnectionStatus();
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }
        disposables.Clear();
        base.Dispose();
    }
}
