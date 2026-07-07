using System.Net;
using MissionPlanner.Library;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.Simulator.SmokeTests;

/// <summary>
/// 
/// </summary>
/// <param name="transport"></param>
public sealed class TransportSmokeTestService(IMavLinkTransport transport) : ITransportSmokeTestService, IAsyncDisposable
{
    /// <inheritdoc/>
    public async Task SendProbeAsync(ReadOnlyMemory<byte> payload, IPEndPoint ipEndPoint, CancellationToken cancellationToken)
    {
        if (!transport.IsConnected)
        {
            await transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        await transport.WriteAsync(payload, ipEndPoint.ToEndPoint(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<TransportSmokeTestResult> WaitForDataAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!transport.IsConnected)
        {
            await transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        using CancellationTokenSource timeoutCts = new(timeout);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var buffer = new byte[1024];

        try
        {
            var result = await transport.ReadAsync(buffer, linkedCts.Token).ConfigureAwait(false);

            var data = buffer.AsMemory(0, result.BytesRead).ToArray();

            return new TransportSmokeTestResult(result.BytesRead, data, result.RemoteEndpoint, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
            when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"No UDP data was received within {timeout}.");
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await transport.DisposeAsync().ConfigureAwait(false);
    }
}
