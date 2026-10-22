using MissionPlanner.Shared.Models.Services.Abstractions;

namespace MissionPlanner.Library.Browser.Transport;

/// <summary>Serial port discovery for the browser app, which has no serial ports.</summary>
public sealed class BrowserSerialPortDiscovery : ISerialPortDiscoveryService
{
    /// <summary>Returns an empty list because the browser has no serial ports.</summary>
    public string[] GetAvailablePorts() => [];
    /// <summary>Always returns false because the browser has no serial ports.</summary>
    public bool IsPortAvailable(string portName) => false;
}
