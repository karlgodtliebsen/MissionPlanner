using MissionPlanner.Firmware.Devices;

namespace MissionPlanner.Firmware.Betaflight.Protocol;

/// <summary>Runs deadline-bounded MSP exchanges without retaining a port or parser between conversations.</summary>
public sealed class BetaflightMspClient(TimeProvider clock) : IBetaflightMspClient
{
    /// <inheritdoc />
    public async Task<MspResponse> RequestAsync(IFirmwareSerialPort port, ushort command, ReadOnlyMemory<byte> payload,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        var request = MspFraming.EncodeRequest(command, payload.Span);
        using var deadline = new CancellationTokenSource(timeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        var parser = new MspFrameParser();
        var written = false;
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            if (!port.IsOpen)
            {
                return new(MspFailure.Disconnected);
            }
            port.DiscardInBuffer();
            await port.Stream.WriteAsync(request, linked.Token).AsTask().WaitAsync(linked.Token).ConfigureAwait(false);
            written = true;
            var buffer = new byte[512];
            while (true)
            {
                var count = await port.Stream.ReadAsync(buffer, linked.Token).AsTask().WaitAsync(linked.Token).ConfigureAwait(false);
                if (count == 0)
                {
                    return new(MspFailure.Disconnected, RequestWritten: written);
                }
                for (var index = 0; index < count; index++)
                {
                    var frame = parser.Push(buffer[index]);
                    if (frame?.Command == command)
                    {
                        return new(frame.IsError ? MspFailure.MspErrorResponse : MspFailure.None, frame, written);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Closing aborts native serial reads that do not honor cancellation.
            await port.DisposeAsync().ConfigureAwait(false);
            return new(cancellationToken.IsCancellationRequested ? MspFailure.Cancelled
                : parser.Failure != MspFailure.None ? parser.Failure : MspFailure.Timeout, RequestWritten: written);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException)
        {
            return new(MspFailure.Disconnected, RequestWritten: written);
        }
    }
}
