using MissionPlanner.Shared.Models.Services.Abstractions;

namespace MissionPlanner.Library.Browser.Transport;

public sealed class BrowserSerialPortDiscovery : ISerialPortDiscoveryService
{
    public string[] GetAvailablePorts() => [];
    public bool IsPortAvailable(string portName) => false;
}
