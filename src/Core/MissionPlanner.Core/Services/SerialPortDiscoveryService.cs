using System.IO.Ports;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Services.Abstractions;

namespace MissionPlanner.Core.Services;

/// <summary>
/// Service for discovering available serial ports for vehicle connections.
/// Uses System.IO.Ports.SerialPort for cross-platform port enumeration.
/// </summary>
public class SerialPortDiscoveryService(ILogger<SerialPortDiscoveryService> logger) : ISerialPortDiscoveryService
{
    /// <inheritdoc/>
    public string[] GetAvailablePorts()
    {
        try
        {
            var ports = SerialPort.GetPortNames();
            logger.LogDebug("Found {PortCount} serial ports: {Ports}", ports.Length, string.Join(", ", ports));
            return ports;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enumerate serial ports");
            return Array.Empty<string>();
        }
    }

    /// <inheritdoc/>
    public bool IsPortAvailable(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return false;
        }

        try
        {
            var availablePorts = SerialPort.GetPortNames();
            var isAvailable = availablePorts.Contains(portName, StringComparer.OrdinalIgnoreCase);
            logger.LogDebug("Port {PortName} availability: {IsAvailable}", portName, isAvailable);
            return isAvailable;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check port {PortName} availability", portName);
            return false;
        }
    }
}
