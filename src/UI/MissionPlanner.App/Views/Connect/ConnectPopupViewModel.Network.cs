using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MissionPlanner.App.Views.Connect;

public partial class ConnectPopupViewModel
{
    /// <summary>Optional local interface for UDP sockets; blank means all interfaces.</summary>
    [ObservableProperty]
    public partial string LocalBindAddress { get; set; } = "";

    /// <summary>UDP Client local port; zero requests an ephemeral port.</summary>
    [ObservableProperty]
    public partial string ClientLocalPort { get; set; } = "0";

    /// <summary>Full WebSocket URL, including any required path or query.</summary>
    [ObservableProperty]
    public partial string WebSocketUrl { get; set; } = "ws://127.0.0.1:8765/";

    /// <summary>Whether serial baud-rate settings apply.</summary>
    public bool ShowBaudRate => SelectedChannel is "AUTO" || IsSerialChannel(SelectedChannel ?? "");

    /// <summary>Whether remote host and port fields apply.</summary>
    public bool ShowRemoteEndpoint => SelectedChannel is "TCP" or "UDPCl";

    /// <summary>Whether UDP local binding settings apply.</summary>
    public bool ShowUdpSettings => SelectedChannel is "UDP" or "UDPCl";

    /// <summary>Whether the local listen port field applies.</summary>
    public bool ShowUdpListen => SelectedChannel is "UDP";

    /// <summary>Whether the optional client local port field applies.</summary>
    public bool ShowUdpClient => SelectedChannel is "UDPCl";

    /// <summary>Whether a complete WebSocket URL is required.</summary>
    public bool ShowWebSocket => SelectedChannel is "WS" or "WSS";

    /// <summary>Explains the selected transport without implying a vehicle is present.</summary>
    public string TransportHelp => SelectedChannel switch
    {
        "UDP" => "Listen for MAVLink on a local UDP port. A vehicle heartbeat is required before connecting.",
        "UDPCl" => "Send MAVLink to a remote UDP listener and receive replies on the same local socket. UDP Client transport is not implemented yet.",
        "WS" or "WSS" => "Connect to a bridge carrying raw binary MAVLink using a full ws:// or wss:// URL. WebSocket transport is not implemented yet.",
        "TCP" => "Connect to a remote TCP MAVLink endpoint.",
        "AUTO" => "Use an available serial controller, or detect an ArduPilot UDP heartbeat when no serial port is available.",
        _ => "Select the controller's serial port and baud rate."
    };

    /// <summary>Actionable validation message; URL values are never echoed.</summary>
    public string NetworkValidationMessage
    {
        get
        {
            if (ShowWebSocket)
            {
                return Uri.TryCreate(WebSocketUrl, UriKind.Absolute, out var uri)
                    && uri.Scheme is "ws" or "wss" && !string.IsNullOrWhiteSpace(uri.Host)
                    && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment)
                    ? "" : "Enter a ws:// or wss:// URL with a host and no embedded credentials or fragment.";
            }
            if (ShowRemoteEndpoint && (string.IsNullOrWhiteSpace(SelectedHost)
                || Uri.CheckHostName(SelectedHost.Trim()) == UriHostNameType.Unknown))
            {
                return "Enter a valid remote hostname or IP address.";
            }
            if ((ShowRemoteEndpoint || ShowUdpListen)
                && (!int.TryParse(SelectedPort, out var port) || port is < 1 or > 65535))
            {
                return "Enter a port from 1 to 65535.";
            }
            if (ShowUdpSettings && !string.IsNullOrWhiteSpace(LocalBindAddress)
                && !IPAddress.TryParse(LocalBindAddress, out _))
            {
                return "Enter a local bind IP address, or leave it blank for all interfaces.";
            }
            if (ShowUdpClient && (!int.TryParse(ClientLocalPort, out var localPort) || localPort is < 0 or > 65535))
            {
                return "Enter a local port from 0 to 65535; zero chooses a free port.";
            }
            return "";
        }
    }

    private void NotifyNetworkFields()
    {
        OnPropertyChanged(nameof(ShowBaudRate));
        OnPropertyChanged(nameof(ShowRemoteEndpoint));
        OnPropertyChanged(nameof(ShowUdpSettings));
        OnPropertyChanged(nameof(ShowUdpListen));
        OnPropertyChanged(nameof(ShowUdpClient));
        OnPropertyChanged(nameof(ShowWebSocket));
        OnPropertyChanged(nameof(TransportHelp));
        OnPropertyChanged(nameof(NetworkValidationMessage));
    }

    private readonly Dictionary<string, (string? Host, string? Port, string Bind, string LocalPort, string Url)> networkDrafts = new();
    private bool changingNetworkDraft;

    partial void OnSelectedChannelChanged(string? oldValue, string? newValue)
    {
        changingNetworkDraft = true;
        try
        {
            if (oldValue is not null)
            {
                networkDrafts[oldValue] = (SelectedHost, SelectedPort, LocalBindAddress, ClientLocalPort, WebSocketUrl);
            }
            if (newValue is not null && networkDrafts.TryGetValue(newValue, out var draft))
            {
                SelectedHost = draft.Host;
                SelectedPort = draft.Port;
                LocalBindAddress = draft.Bind;
                ClientLocalPort = draft.LocalPort;
                WebSocketUrl = draft.Url;
            }
            else if (oldValue is not null && !(oldValue == "AUTO" && newValue == "UDP"))
            {
                SelectedHost = "127.0.0.1";
                SelectedPort = newValue == "UDPCl" ? "14560" : newValue == "TCP" ? "5760" : "14550";
                LocalBindAddress = "";
                ClientLocalPort = "0";
                WebSocketUrl = newValue == "WSS" ? "wss://127.0.0.1:8765/" : "ws://127.0.0.1:8765/";
            }
        }
        finally
        {
            changingNetworkDraft = false;
        }
        NotifyNetworkFields();
    }
}
