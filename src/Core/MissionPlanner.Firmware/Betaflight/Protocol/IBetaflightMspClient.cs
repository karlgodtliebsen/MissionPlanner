using MissionPlanner.Firmware.Devices;

namespace MissionPlanner.Firmware.Betaflight.Protocol;

/// <summary>Performs sequential requests on a caller-owned exclusive serial conversation.</summary>
public interface IBetaflightMspClient
{
    /// <summary>Requests one command. The caller must not share the port with another reader or request.</summary>
    Task<MspResponse> RequestAsync(IFirmwareSerialPort port, ushort command, ReadOnlyMemory<byte> payload,
        TimeSpan timeout, CancellationToken cancellationToken = default);
}
