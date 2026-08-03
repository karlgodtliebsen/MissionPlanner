using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.MavFtp.Abstractions;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.MavLink.Services;

/// <summary>
/// Represents a session for a vehicle connection, managing its state and handling updates.
/// </summary>
public interface IMavLinkConnectionSessionFactory
{
    /// <summary>
    /// Creates a serial connection to a vehicle using the specified port name and baud rate. Optionally, a configuration action can be provided to customize the transport endpoint settings. The connection process is cancellable via the provided cancellation token.
    /// </summary>
    /// <param name="transportOptions"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The created MAVLink connection session.</returns>
    Task<IMavLinkConnectionSession> CreateSerialConnection(IOptions<TransportEndpoint> transportOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a TCP connection to a vehicle using the specified port and host. Optionally, a configuration action can be provided to customize the transport endpoint settings. The connection process is cancellable via the provided cancellation token.
    /// </summary>
    /// <param name="transportOptions"></param>
    /// <param name="cancellationToken">A cancellation token to cancel the connection process.</param>
    /// <returns>The created MAVLink connection session.</returns>
    Task<IMavLinkConnectionSession> CreateTcpConnection(IOptions<TransportEndpoint> transportOptions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a UDP connection to a vehicle using the specified local port and optional remote host and port. Optionally, a configuration action can be provided to customize the transport endpoint settings. The connection process is cancellable via the provided cancellation token.
    /// </summary>
    /// <param name="transportOptions"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The created MAVLink connection session.</returns>
    Task<IMavLinkConnectionSession> CreateUdpConnection(IOptions<TransportEndpoint> transportOptions, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a session for a vehicle connection, managing its state and handling updates.
/// </summary>
public interface IMavLinkConnectionSession : IAsyncDisposable
{
    /// <summary>
    /// Creates a MAVLink FTP client. Returns null if the client cannot be created.
    /// </summary>
    /// <returns>The created MAVLink FTP client or null.</returns>
    IMavFtpClient? CreateMavFtpClient();

    /// <summary>
    /// Gets the established MAVLink connection. Throws an exception if no connection is established.
    /// </summary>
    IMavLinkConnection Connection { get; }

    /// <summary>
    /// Gets the cancellation token source for the connection session.
    /// </summary>
    CancellationTokenSource CancellationTokenSource { get; }

    /// <summary>
    /// Gets the established MAVLink transport. Throws an exception if no transport is established.
    /// </summary>
    IMavLinkTransport Transport { get; }

    /// <summary>
    /// Gets the established MAVLink client. Throws an exception if no client is established.
    /// </summary>
    IMavLinkClient Client { get; }

    /// <summary>
    /// Internal disconnect method - must be called with connectionLock held or from single-threaded context
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a session for a connection, managing its state and handling updates.
/// </summary>
/// <param name="domainFactory"></param>
/// <param name="serviceFactory"></param>
/// <param name="logger"></param>
public sealed class MavLinkConnectionSessionFactory(IDomainFactory domainFactory, IServiceFactory serviceFactory, ILogger<MavLinkConnectionSession> logger) :
    IMavLinkConnectionSessionFactory
{
    /// <summary>
    /// Creates a serial connection to a vehicle using the specified port name and baud rate. Optionally, a configuration action can be provided to customize the transport endpoint settings. The connection process is cancellable via the provided cancellation token.
    /// </summary>
    /// <param name="transportOptions"></param>
    /// <param name="cancellationToken"></param>
    public Task<IMavLinkConnectionSession> CreateSerialConnection(IOptions<TransportEndpoint> transportOptions, CancellationToken cancellationToken = default)
    {
        var serviceCts = new CancellationTokenSource();

        // Create serial transport
        var transport = domainFactory.Create<ISerialMavLinkTransport, IOptions<TransportEndpoint>>(transportOptions);
        // Create MAVLink client
        var client = domainFactory.Create<IMavLinkClient, ISerialMavLinkTransport>(transport);

        var connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);

        var connectionTask = Task.Run(() => connection.StartAsync(linkedCts.Token), linkedCts.Token);

        var session = domainFactory.Create<IMavLinkConnectionSession, IMavLinkTransport, IMavLinkClient, IMavLinkConnection, CancellationTokenSource, Task>(transport, client, connection, serviceCts, connectionTask);
        return Task.FromResult(session);
    }


    /// <inheritdoc/>
    public Task<IMavLinkConnectionSession> CreateTcpConnection(IOptions<TransportEndpoint> transportOptions, CancellationToken cancellationToken = default)
    {
        var serviceCts = new CancellationTokenSource();

        // Create TCP transport
        var transport = domainFactory.Create<ITcpMavLinkTransport, IOptions<TransportEndpoint>>(transportOptions);
        // Create MAVLink client
        var client = domainFactory.Create<IMavLinkClient, ITcpMavLinkTransport>(transport);

        var connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);

        var connectionTask = Task.Run(() => connection.StartAsync(linkedCts.Token), linkedCts.Token);
        var session = domainFactory.Create<IMavLinkConnectionSession, IMavLinkTransport, IMavLinkClient, IMavLinkConnection, CancellationTokenSource, Task>(transport, client, connection, serviceCts, connectionTask);
        return Task.FromResult(session);
    }


    /// <inheritdoc/>
    public Task<IMavLinkConnectionSession> CreateUdpConnection(IOptions<TransportEndpoint> transportOptions, CancellationToken cancellationToken = default)
    {
        var serviceCts = new CancellationTokenSource();
        // Create UDP transport
        var transport = domainFactory.Create<IUdpMavLinkTransport, IOptions<TransportEndpoint>>(transportOptions);
        // Create MAVLink client
        var client = domainFactory.Create<IMavLinkClient, IUdpMavLinkTransport>(transport);
        var connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);
        var connectionTask = Task.Run(() => connection.StartAsync(linkedCts.Token), linkedCts.Token);
        var session = domainFactory.Create<IMavLinkConnectionSession, IMavLinkTransport, IMavLinkClient, IMavLinkConnection, CancellationTokenSource, Task>(transport, client, connection, serviceCts, connectionTask);
        return Task.FromResult(session);
    }
}

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
