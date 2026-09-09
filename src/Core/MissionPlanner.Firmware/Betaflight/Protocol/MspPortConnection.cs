using MissionPlanner.Firmware.Devices;

namespace MissionPlanner.Firmware.Betaflight.Protocol;

/// <summary>A typed exclusive serial open result. The caller disposes the successful connection.</summary>
public sealed record MspPortConnection(MspFailure Failure, IFirmwareSerialPort? Port = null) : IAsyncDisposable
{
    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return Port?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}