using Microsoft.Extensions.Logging;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.MavFtp.Abstractions;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.MavLink.Services;

/// <summary>
/// Represents a session for a connection, managing its state and handling updates.
/// </summary>
/// <param name="domainFactory"></param>
/// <param name="connection"></param>
/// <param name="client"></param>
/// <param name="transport"></param>
/// <param name="cancellationTokenSource"></param>
/// <param name="connectionTask"></param>
/// <param name="logger"></param>
public sealed class MavLinkConnectionSession(
    IDomainFactory domainFactory,
    IMavLinkConnection connection,
    IMavLinkClient client,
    IMavLinkTransport transport,
    CancellationTokenSource cancellationTokenSource,
    Task connectionTask,
    ILogger<MavLinkConnectionSession> logger)
    : IMavLinkConnectionSession
{
    private bool isDisposed;

    /// <inheritdoc />
    public CancellationTokenSource CancellationTokenSource => cancellationTokenSource;

    /// <summary>
    /// Gets the established MAVLink connection. Throws an exception if no connection is established.
    /// </summary>
    public IMavLinkConnection Connection => connection;

    /// <summary>
    /// Gets the established MAVLink client. Throws an exception if no client is established.
    /// </summary>
    public IMavLinkClient Client => client;

    /// <summary>
    /// Gets the established MAVLink transport. Throws an exception if no transport is established.
    /// </summary>
    public IMavLinkTransport Transport => transport;

    /// <inheritdoc />
    public IMavFtpClient? CreateMavFtpClient()
    {
        var mavClient = domainFactory.Create<IMavFtpClient, IMavLinkConnection>(Connection);
        return mavClient;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        await CancellationTokenSource.CancelAsync().ConfigureAwait(false);

        // Stop and dispose services
        try
        {
            await Connection.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Non Critical Failure Disposing connection ");
        }


        // Stop and disconnect transport
        try
        {
            await Transport.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Non Critical Failure Disposing transport ");
        }

        // Stop and disconnect client
        try
        {
            await Client.StopAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Non Critical Failure Disposing client ");
        }
    }

    /// <summary>
    /// Internal disconnect method - must be called with connectionLock held or from single-threaded context
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Stop background tasks gracefully. Cancel first; otherwise the wait below just waits for the timeout.
            await CancellationTokenSource.CancelAsync().ConfigureAwait(false);
            var tasksToWait = new List<Task>();
            if (!connectionTask.IsCompleted)
            {
                tasksToWait.Add(connectionTask);
            }

            if (tasksToWait.Any())
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await Task.WhenAll(tasksToWait).WaitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("Background tasks did not complete within timeout period during disconnect");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error waiting for background tasks to complete");
                }
            }

            await DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while disconnecting Session");
        }

        logger.LogInformation("Successfully disconnected");
    }
}
