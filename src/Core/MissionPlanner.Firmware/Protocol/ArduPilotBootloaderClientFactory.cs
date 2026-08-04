using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Devices;

namespace MissionPlanner.Firmware.Protocol;

/// <summary>Creates bootloader clients from host services.</summary>
public sealed class ArduPilotBootloaderClientFactory(IOptions<FirmwareOptions> options, TimeProvider timeProvider, ILoggerFactory loggerFactory) : IArduPilotBootloaderClientFactory
{
    /// <inheritdoc />
    public IArduPilotBootloaderClient Create(IFirmwareSerialPort port)
    {
        return new ArduPilotBootloaderClient(port, options, timeProvider, loggerFactory.CreateLogger<ArduPilotBootloaderClient>());
    }
}
