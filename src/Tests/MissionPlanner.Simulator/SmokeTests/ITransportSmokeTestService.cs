using System.Net;

namespace MissionPlanner.Simulator.SmokeTests;

/// <summary>
/// Service for performing transport smoke tests.
/// </summary>
public interface ITransportSmokeTestService
{
    /// <summary>
    /// Sends a probe payload to the transport endpoint.
    /// </summary>
    /// <param name="payload"></param>
    /// <param name="ipEndPoint"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task SendProbeAsync(ReadOnlyMemory<byte> payload, IPEndPoint ipEndPoint, CancellationToken cancellationToken);

    /// <summary>
    /// Waits for data to be received from the transport endpoint.
    /// </summary>
    /// <param name="timeout">The maximum time to wait for data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the transport smoke test.</returns>
    Task<TransportSmokeTestResult> WaitForDataAsync(TimeSpan timeout, CancellationToken cancellationToken);
}