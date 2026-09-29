using System.Net;

namespace MissionPlanner.Core.Vehicles;

/// <summary>Explicit network connection settings; protocol values remain compatible with saved channel IDs.</summary>
public sealed record NetworkConnectionSettings
{
    /// <summary>UDP, UDPCl, WS or WSS.</summary>
    public string Channel { get; init; } = "UDP";
    /// <summary>UDP Client remote hostname or IP address.</summary>
    public string RemoteHost { get; init; } = "127.0.0.1";
    /// <summary>UDP Client remote port.</summary>
    public int RemotePort { get; init; } = 14560;
    /// <summary>Local port; zero is allowed only for UDP Client.</summary>
    public int LocalPort { get; init; } = 14550;
    /// <summary>Local bind interface; blank means any compatible interface.</summary>
    public string LocalBindAddress { get; init; } = "";
    /// <summary>Full request URL; never log this property.</summary>
    public string WebSocketUrl { get; init; } = "ws://127.0.0.1:8765/";

    /// <summary>Returns an error without echoing input or credentials.</summary>
    public string? Validate()
    {
        if (Channel is "WS" or "WSS")
        {
            return Uri.TryCreate(WebSocketUrl, UriKind.Absolute, out var uri)
                && uri.Scheme is "ws" or "wss" && !string.IsNullOrWhiteSpace(uri.Host)
                && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment)
                ? null : "Enter a ws:// or wss:// URL without embedded credentials or a fragment.";
        }
        if (Channel is not ("UDP" or "UDPCl"))
        {
            return "Unsupported network channel.";
        }
        if (LocalPort < (Channel == "UDPCl" ? 0 : 1) || LocalPort > 65535)
        {
            return "Invalid local UDP port.";
        }
        if (!string.IsNullOrWhiteSpace(LocalBindAddress) && !IPAddress.TryParse(LocalBindAddress, out _))
        {
            return "The local bind address must be an IP address.";
        }
        if (Channel == "UDPCl" && (RemotePort is < 1 or > 65535
            || string.IsNullOrWhiteSpace(RemoteHost) || Uri.CheckHostName(RemoteHost) == UriHostNameType.Unknown))
        {
            return "UDP Client requires a valid remote host and port.";
        }
        return null;
    }

    /// <summary>Endpoint description with WebSocket path and query omitted.</summary>
    public string Description => Channel is "WS" or "WSS"
        ? Uri.TryCreate(WebSocketUrl, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Authority).Replace(uri.UserInfo + "@", "") : "WebSocket"
        : Channel == "UDPCl" ? $"UDP Client {RemoteHost}:{RemotePort} (local {LocalBindAddress}:{LocalPort})"
        : $"UDP Listen {LocalBindAddress}:{LocalPort}";

    /// <inheritdoc />
    public override string ToString() => Description;
}
