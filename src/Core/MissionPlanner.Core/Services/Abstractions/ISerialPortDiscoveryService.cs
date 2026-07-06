namespace MissionPlanner.Core.Services.Abstractions;

/// <summary>
/// Service for discovering available serial ports for vehicle connections.
/// </summary>
public interface ISerialPortDiscoveryService
{
    /// <summary>
    /// Gets a list of available serial port names on the system.
    /// </summary>
    /// <returns>Array of port names (e.g., "COM3", "/dev/ttyUSB0")</returns>
    string[] GetAvailablePorts();

    /// <summary>
    /// Checks if a specific port is available.
    /// </summary>
    /// <param name="portName">The port name to check</param>
    /// <returns>True if the port exists and is available, false otherwise</returns>
    bool IsPortAvailable(string portName);
}
