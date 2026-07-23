using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Simulation;

/// <summary>Associates one simulator session and vehicle with its exact MAVLink connection services.</summary>
/// <param name="SessionId">Owning simulation session identity.</param>
/// <param name="VehicleId">Exact connected vehicle identity.</param>
/// <param name="ConnectionSession">Independently owned connection session.</param>
/// <param name="Profile">Allocated simulator profile.</param>
/// <param name="StartedAt">Connection readiness timestamp.</param>
public sealed record SimulationVehicleChannel(
    Guid SessionId,
    VehicleId VehicleId,
    IVehicleConnectionSession ConnectionSession,
    SimulatorProfile Profile,
    DateTimeOffset StartedAt);

/// <summary>Routes vehicle operations to independently owned simulator transports.</summary>
public interface ISimulationVehicleChannelRegistry
{
    /// <summary>Registers one exact session and vehicle channel.</summary>
    /// <param name="channel">The channel to register.</param>
    void Register(SimulationVehicleChannel channel);

    /// <summary>Finds a channel by exact vehicle identity.</summary>
    /// <param name="vehicleId">Vehicle identity.</param>
    /// <returns>The channel, or <see langword="null"/>.</returns>
    SimulationVehicleChannel? Find(VehicleId vehicleId);

    /// <summary>Finds a channel by exact simulation session identity.</summary>
    /// <param name="sessionId">Simulation session identity.</param>
    /// <returns>The channel, or <see langword="null"/>.</returns>
    SimulationVehicleChannel? Find(Guid sessionId);

    /// <summary>Removes only the channel owned by an exact session.</summary>
    /// <param name="sessionId">Simulation session identity.</param>
    /// <returns>The removed channel, or <see langword="null"/>.</returns>
    SimulationVehicleChannel? Remove(Guid sessionId);
}

/// <summary>Maintains exact session and vehicle routes for concurrent simulator connections.</summary>
public sealed class SimulationVehicleChannelRegistry : ISimulationVehicleChannelRegistry
{
    private readonly ConcurrentDictionary<Guid, SimulationVehicleChannel> bySession = [];
    private readonly ConcurrentDictionary<VehicleId, SimulationVehicleChannel> byVehicle = [];

    /// <inheritdoc />
    public void Register(SimulationVehicleChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        if (!bySession.TryAdd(channel.SessionId, channel))
        {
            throw new InvalidOperationException($"Simulation session {channel.SessionId} already has a vehicle channel.");
        }

        if (byVehicle.TryAdd(channel.VehicleId, channel))
        {
            return;
        }

        bySession.TryRemove(channel.SessionId, out _);
        throw new InvalidOperationException($"Vehicle {channel.VehicleId} is already routed to another connection.");
    }

    /// <inheritdoc />
    public SimulationVehicleChannel? Find(VehicleId vehicleId) =>
        byVehicle.TryGetValue(vehicleId, out var channel) ? channel : null;

    /// <inheritdoc />
    public SimulationVehicleChannel? Find(Guid sessionId) =>
        bySession.TryGetValue(sessionId, out var channel) ? channel : null;

    /// <inheritdoc />
    public SimulationVehicleChannel? Remove(Guid sessionId)
    {
        if (!bySession.TryRemove(sessionId, out var channel))
        {
            return null;
        }

        byVehicle.TryRemove(new KeyValuePair<VehicleId, SimulationVehicleChannel>(channel.VehicleId, channel));
        return channel;
    }
}

/// <summary>Creates independently owned MAVLink sessions for direct SITL runtimes.</summary>
public sealed class SimulatorVehicleConnectionFactory(
    IVehicleParameterRegistry parameterRegistry,
    IDomainFactory domainFactory,
    IServiceFactory serviceFactory,
    IDomainEventHub domainEventHub,
    IDateTimeProvider clock,
    IVehicleMessagePumpCoordinator messagePumpCoordinator,
    IVehicleRegistry vehicleRegistry,
    ISimulationVehicleChannelRegistry channelRegistry,
    ILoggerFactory loggerFactory) : ISimulatorVehicleConnectionFactory
{
    /// <inheritdoc />
    public ISimulatorVehicleConnection Create(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("A simulator connection requires a non-empty session identity.", nameof(sessionId));
        }

        var session = new VehicleConnectionSession(
            parameterRegistry,
            domainFactory,
            serviceFactory,
            domainEventHub,
            clock,
            loggerFactory.CreateLogger<VehicleConnectionSession>(),
            messagePumpCoordinator,
            resetRegistryOnLifecycle: false);
        return new IsolatedSimulatorVehicleConnection(
            sessionId,
            session,
            vehicleRegistry,
            domainEventHub,
            clock,
            domainFactory,
            channelRegistry,
            loggerFactory.CreateLogger<IsolatedSimulatorVehicleConnection>());
    }
}

/// <summary>Connects one SITL endpoint without replacing any application or simulator connection.</summary>
public sealed class IsolatedSimulatorVehicleConnection : ISimulatorVehicleConnection
{
    private readonly Guid sessionId;
    private readonly IVehicleConnectionSession connectionSession;
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IDomainEventHub domainEventHub;
    private readonly IDateTimeProvider clock;
    private readonly IDomainFactory domainFactory;
    private readonly ISimulationVehicleChannelRegistry channelRegistry;
    private readonly ILogger<IsolatedSimulatorVehicleConnection> logger;
    private readonly SemaphoreSlim gate = new(1, 1);
    private VehicleId? vehicleId;

    /// <summary>Initializes one exact, independently owned simulator connection.</summary>
    /// <param name="sessionId">Owning simulation session identity.</param>
    /// <param name="connectionSession">Independent MAVLink connection session.</param>
    /// <param name="vehicleRegistry">Shared multi-vehicle registry.</param>
    /// <param name="domainEventHub">Domain event hub.</param>
    /// <param name="clock">Application clock.</param>
    /// <param name="domainFactory">Domain service factory.</param>
    /// <param name="channelRegistry">Exact vehicle-channel registry.</param>
    /// <param name="logger">Logger.</param>
    public IsolatedSimulatorVehicleConnection(
        Guid sessionId,
        IVehicleConnectionSession connectionSession,
        IVehicleRegistry vehicleRegistry,
        IDomainEventHub domainEventHub,
        IDateTimeProvider clock,
        IDomainFactory domainFactory,
        ISimulationVehicleChannelRegistry channelRegistry,
        ILogger<IsolatedSimulatorVehicleConnection> logger)
    {
        this.sessionId = sessionId;
        this.connectionSession = connectionSession;
        this.vehicleRegistry = vehicleRegistry;
        this.domainEventHub = domainEventHub;
        this.clock = clock;
        this.domainFactory = domainFactory;
        this.channelRegistry = channelRegistry;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<VehicleId> ConnectAsync(
        SimulatorProfile profile,
        SimulationEndpoint endpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(endpoint);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (vehicleId is not null)
            {
                throw new SimulationConnectionException("This simulator runtime already owns a vehicle connection.");
            }

            var expectedSystemId = profile.EffectiveLaunchSettings.SystemId;
            var registered = new TaskCompletionSource<VehicleId>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = domainEventHub.SubscribeDomainEventAsync<VehicleRegistered>((message, _) =>
            {
                if (message.VehicleId.SystemId == expectedSystemId)
                {
                    registered.TrySetResult(message.VehicleId);
                }

                return Task.CompletedTask;
            });

            try
            {
                await connectionSession.CreateUdpConnection(
                    endpoint.Port,
                    endpoint.Host,
                    endpoint.Port,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                var connectedVehicle = await registered.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
                var state = vehicleRegistry.GetRequired(connectedVehicle)?.State;
                if (state?.Identity.Firmware.Family != profile.FirmwareFamily)
                {
                    throw new SimulationConnectionException(
                        $"Received heartbeat {connectedVehicle} for {state?.Identity.Firmware.Family.ToString() ?? "unknown firmware"}; " +
                        $"expected system {expectedSystemId} and {profile.FirmwareFamily}.");
                }

                channelRegistry.Register(new SimulationVehicleChannel(
                    sessionId,
                    connectedVehicle,
                    connectionSession,
                    profile,
                    clock.UtcNow));
                vehicleId = connectedVehicle;
                await domainEventHub.PublishDomainEventAsync(
                    new VehicleConnected(connectedVehicle, "Simulation UDP", endpoint.DisplayText, clock.UtcNow),
                    cancellationToken).ConfigureAwait(false);
                await RequestInitialTelemetryAsync(connectedVehicle, cancellationToken).ConfigureAwait(false);
                logger.LogInformation(
                    "Connected isolated simulation session {SessionId} to {VehicleId} on {Endpoint}.",
                    sessionId,
                    connectedVehicle,
                    endpoint.DisplayText);
                return connectedVehicle;
            }
            catch (TimeoutException)
            {
                await DisconnectCoreAsync(CancellationToken.None).ConfigureAwait(false);
                throw new SimulationConnectionException(
                    $"The simulator process started, but system {expectedSystemId} did not send a heartbeat within {timeout.TotalSeconds:0.#} seconds.");
            }
            catch
            {
                await DisconnectCoreAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisconnectCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RequestInitialTelemetryAsync(VehicleId connectedVehicle, CancellationToken cancellationToken)
    {
        try
        {
            var commands = domainFactory.Create<IMavLinkCommandService, MissionPlanner.MavLink.Client.IMavLinkClient>(connectionSession.Client);
            await commands.RequestAutopilotVersionAsync(connectedVehicle, cancellationToken).ConfigureAwait(false);
            await commands.RequestDataStreamAsync(connectedVehicle, MavDataStream.Extra1, 10, true, cancellationToken).ConfigureAwait(false);
            await commands.RequestDataStreamAsync(connectedVehicle, MavDataStream.Position, 5, true, cancellationToken).ConfigureAwait(false);
            await commands.RequestDataStreamAsync(connectedVehicle, MavDataStream.ExtendedStatus, 2, true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Initial telemetry request failed for simulation vehicle {VehicleId}.", connectedVehicle);
        }
    }

    private async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        var exactVehicle = vehicleId;
        channelRegistry.Remove(sessionId);
        vehicleId = null;
        await connectionSession.DisconnectAsync(exactVehicle, cancellationToken).ConfigureAwait(false);
    }
}
