using MissionPlanner.Firmware.Devices;

namespace MissionPlanner.Firmware.Protocol;

/// <summary>Creates a protocol client that assumes ownership of an open firmware port.</summary>
public interface IArduPilotBootloaderClientFactory
{
    /// <summary>Creates a client that disposes the supplied port.</summary>
    IArduPilotBootloaderClient Create(IFirmwareSerialPort port);
}
