namespace MissionPlanner.Transport.Abstractions;

/// <summary>
/// Represents a MAVLink transport that communicates over a serial port.
/// </summary>
public interface ISerialMavLinkTransport : IMavLinkTransport
{
    /// <summary>Gets the operating-system serial device name.</summary>
    string PortName { get; }
}
