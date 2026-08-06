namespace MissionPlanner.Simulation.ArduPilot;

/// <summary>Identifies a supported direct-SITL serial endpoint transport.</summary>
public enum ArduPilotSerialTransport
{
    /// <summary>SITL sends UDP datagrams to the configured endpoint.</summary>
    UdpClient,

    /// <summary>SITL establishes a TCP client connection to the configured endpoint.</summary>
    TcpClient
}
