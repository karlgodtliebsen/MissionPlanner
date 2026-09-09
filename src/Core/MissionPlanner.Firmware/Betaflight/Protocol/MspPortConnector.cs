using MissionPlanner.Firmware.Devices;

namespace MissionPlanner.Firmware.Betaflight.Protocol;

/// <summary>Opens the existing platform firmware serial adapter with a finite deadline.</summary>
public sealed class MspPortConnector(IFirmwareSerialPortFactory ports, TimeProvider clock)
{
    /// <summary>Acquires exclusive ownership or returns a typed failure without leaking late opens.</summary>
    public async Task<MspPortConnection> OpenAsync(string portName, CancellationToken cancellationToken = default)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        Task<IFirmwareSerialPort>? pending = null;
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            pending = ports.OpenAsync(new SerialPortOpenOptions(portName), linked.Token);
            var port = await pending.WaitAsync(linked.Token).ConfigureAwait(false);
            return new(MspFailure.None, port);
        }
        catch (OperationCanceledException)
        {
            if (pending is not null)
            {
                _ = CloseLateOpenAsync(pending);
            }
            return new(cancellationToken.IsCancellationRequested ? MspFailure.Cancelled : MspFailure.Timeout);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new(MspFailure.PortUnavailableOrBusy);
        }
        catch (NotSupportedException)
        {
            return new(MspFailure.Unsupported);
        }
    }

    private static async Task CloseLateOpenAsync(Task<IFirmwareSerialPort> pending)
    {
        try
        {
            var port = await pending.ConfigureAwait(false);
            await port.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Observe failed opens after the caller has already received its typed outcome.
        }
    }
}